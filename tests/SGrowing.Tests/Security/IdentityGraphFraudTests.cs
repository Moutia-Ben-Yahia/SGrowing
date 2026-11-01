using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.GraphFraud;
using Xunit;

namespace SGrowing.Tests.Security;

public class IdentityGraphFraudTests
{
    private readonly IdentityGraphEngine _engine = new();
    private readonly GraphClusterAnalyzerService _analyzer;
    private readonly FraudRingContainmentService _containment;

    public IdentityGraphFraudTests()
    {
        _analyzer = new GraphClusterAnalyzerService(_engine);
        _containment = new FraudRingContainmentService(_engine, _analyzer);
    }

    [Fact]
    public void AddNode_ValidNode_AddsAndReturnsNode()
    {
        // Arrange
        var node = new GraphNode
        {
            NodeId = "customer:cust_1001",
            NodeType = GraphNodeType.Customer,
            Properties = new Dictionary<string, string> { ["tier"] = "vip" },
            RiskScore = 15.0
        };

        // Act
        var added = _engine.AddOrUpdateNode(node);
        var retrieved = _engine.GetNode("customer:cust_1001");

        // Assert
        added.Should().NotBeNull();
        retrieved.Should().NotBeNull();
        retrieved!.NodeId.Should().Be("customer:cust_1001");
        retrieved.NodeType.Should().Be(GraphNodeType.Customer);
        retrieved.Properties["tier"].Should().Be("vip");
        retrieved.RiskScore.Should().Be(15.0);
        retrieved.IsQuarantined.Should().BeFalse();
        _engine.NodeCount.Should().Be(1);
    }

    [Fact]
    public void AddOrUpdateNode_ExistingNode_UpdatesPropertiesAndPreservesQuarantined()
    {
        // Arrange
        var initial = new GraphNode
        {
            NodeId = "customer:cust_1002",
            NodeType = GraphNodeType.Customer,
            Properties = new Dictionary<string, string> { ["tier"] = "standard" },
            RiskScore = 20.0,
            IsQuarantined = true
        };
        _engine.AddOrUpdateNode(initial);

        var update = new GraphNode
        {
            NodeId = "customer:cust_1002",
            NodeType = GraphNodeType.Customer,
            Properties = new Dictionary<string, string> { ["email"] = "cust1002@example.com" },
            RiskScore = 45.0,
            IsQuarantined = false
        };

        // Act
        var result = _engine.AddOrUpdateNode(update);
        var retrieved = _engine.GetNode("customer:cust_1002");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Properties.Should().ContainKey("tier").WhoseValue.Should().Be("standard");
        retrieved.Properties.Should().ContainKey("email").WhoseValue.Should().Be("cust1002@example.com");
        retrieved.RiskScore.Should().Be(45.0);
        retrieved.IsQuarantined.Should().BeTrue("quarantine state must be preserved once flagged");
    }

    [Fact]
    public void AddOrUpdateNode_ExistingNode_TakesMaxRiskScore()
    {
        // Arrange
        _engine.AddOrUpdateNode(new GraphNode
        {
            NodeId = "customer:cust_1003",
            NodeType = GraphNodeType.Customer,
            RiskScore = 80.0
        });

        // Act
        _engine.AddOrUpdateNode(new GraphNode
        {
            NodeId = "customer:cust_1003",
            NodeType = GraphNodeType.Customer,
            RiskScore = 30.0
        });

        var retrieved = _engine.GetNode("customer:cust_1003");

        // Assert
        retrieved!.RiskScore.Should().Be(80.0);
    }

    [Fact]
    public void AddEdge_NewEdge_AddsToBothOutAndInAdjacency()
    {
        // Arrange
        var edge = new GraphEdge
        {
            SourceNodeId = "customer:cust_2001",
            TargetNodeId = "device:fp_dev999",
            RelationType = GraphRelationType.PlacedFromDevice,
            Weight = 1.0
        };

        // Act
        _engine.AddEdge(edge);

        // Assert
        var outEdges = _engine.GetOutEdges("customer:cust_2001");
        var inEdges = _engine.GetInEdges("device:fp_dev999");

        outEdges.Should().HaveCount(1);
        outEdges[0].TargetNodeId.Should().Be("device:fp_dev999");
        outEdges[0].RelationType.Should().Be(GraphRelationType.PlacedFromDevice);

        inEdges.Should().HaveCount(1);
        inEdges[0].SourceNodeId.Should().Be("customer:cust_2001");
        _engine.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void AddEdge_DuplicateEdge_AggregatesWeight()
    {
        // Arrange
        var edge1 = new GraphEdge
        {
            SourceNodeId = "customer:cust_2002",
            TargetNodeId = "device:fp_dev888",
            RelationType = GraphRelationType.PlacedFromDevice,
            Weight = 1.5
        };
        var edge2 = new GraphEdge
        {
            SourceNodeId = "customer:cust_2002",
            TargetNodeId = "device:fp_dev888",
            RelationType = GraphRelationType.PlacedFromDevice,
            Weight = 2.5
        };

        // Act
        _engine.AddEdge(edge1);
        _engine.AddEdge(edge2);

        // Assert
        var outEdges = _engine.GetOutEdges("customer:cust_2002");
        outEdges.Should().HaveCount(1);
        outEdges[0].Weight.Should().Be(4.0);
        _engine.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void GetOutEdges_And_GetInEdges_ReturnExpectedIncidentEdges()
    {
        // Arrange
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_3001",
            TargetNodeId = "device:fp_abc",
            RelationType = GraphRelationType.PlacedFromDevice
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_3001",
            TargetNodeId = "phone:+212600000001",
            RelationType = GraphRelationType.RegisteredWithPhone
        });

        // Act
        var outEdges = _engine.GetOutEdges("customer:cust_3001");
        var inEdges = _engine.GetInEdges("device:fp_abc");

        // Assert
        outEdges.Should().HaveCount(2);
        inEdges.Should().HaveCount(1);
        inEdges[0].SourceNodeId.Should().Be("customer:cust_3001");
    }

    [Fact]
    public void GetNeighbors_1Hop_ReturnsImmediateNeighbors()
    {
        // Arrange
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_4001",
            TargetNodeId = "device:fp_shared",
            RelationType = GraphRelationType.PlacedFromDevice
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_4002",
            TargetNodeId = "device:fp_shared",
            RelationType = GraphRelationType.PlacedFromDevice
        });

        // Act
        var neighbors = _engine.GetNeighbors("customer:cust_4001", maxHops: 1);

        // Assert
        neighbors.Should().ContainSingle(n => n.NodeId == "device:fp_shared");
    }

    [Fact]
    public void GetNeighbors_2Hops_TraversesThroughIntermediateNode()
    {
        // Arrange
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_5001",
            TargetNodeId = "device:fp_common",
            RelationType = GraphRelationType.PlacedFromDevice
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_5002",
            TargetNodeId = "device:fp_common",
            RelationType = GraphRelationType.PlacedFromDevice
        });

        // Act
        var neighbors = _engine.GetNeighbors("customer:cust_5001", maxHops: 2);

        // Assert
        neighbors.Should().Contain(n => n.NodeId == "device:fp_common");
        neighbors.Should().Contain(n => n.NodeId == "customer:cust_5002");
    }

    [Fact]
    public void GetNeighbors_NonExistentNode_ReturnsEmptyList()
    {
        // Act
        var neighbors = _engine.GetNeighbors("customer:ghost_node");

        // Assert
        neighbors.Should().BeEmpty();
    }

    [Fact]
    public void GetSubGraph_Depth1_ReturnsEgoCenterAndDirectNeighbors()
    {
        // Arrange
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_6001",
            TargetNodeId = "device:fp_ego1",
            RelationType = GraphRelationType.PlacedFromDevice
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_6001",
            TargetNodeId = "phone:+212611111111",
            RelationType = GraphRelationType.RegisteredWithPhone
        });

        // Act
        var (nodes, edges) = _engine.GetSubGraph("customer:cust_6001", depth: 1);

        // Assert
        nodes.Should().HaveCount(3);
        nodes.Should().Contain(n => n.NodeId == "customer:cust_6001");
        nodes.Should().Contain(n => n.NodeId == "device:fp_ego1");
        nodes.Should().Contain(n => n.NodeId == "phone:+212611111111");
        edges.Should().HaveCount(2);
    }

    [Fact]
    public void GetSubGraph_Depth2_ReturnsTwoHopNeighborhoodAndInterconnectingEdges()
    {
        // Arrange
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_7001",
            TargetNodeId = "device:fp_shared_hub",
            RelationType = GraphRelationType.PlacedFromDevice
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_7002",
            TargetNodeId = "device:fp_shared_hub",
            RelationType = GraphRelationType.PlacedFromDevice
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_7002",
            TargetNodeId = "rib:RIB999000111",
            RelationType = GraphRelationType.TransferredToBank
        });

        // Act
        var (nodes, edges) = _engine.GetSubGraph("customer:cust_7001", depth: 2);

        // Assert
        nodes.Should().Contain(n => n.NodeId == "customer:cust_7001");
        nodes.Should().Contain(n => n.NodeId == "device:fp_shared_hub");
        nodes.Should().Contain(n => n.NodeId == "customer:cust_7002");
        edges.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetSubGraph_NonExistentNode_ReturnsEmptyCollections()
    {
        // Act
        var (nodes, edges) = _engine.GetSubGraph("customer:unknown_id", depth: 2);

        // Assert
        nodes.Should().BeEmpty();
        edges.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeAddress_StripsPunctuationAndExpandsAbbreviations()
    {
        // Arrange
        var raw = "123 Main St., Apt. #4B, Casablanca";

        // Act
        var normalized = _engine.NormalizeAddress(raw);

        // Assert
        normalized.Should().Be("123 main street apartment 4b casablanca");
    }

    [Fact]
    public void NormalizeAddress_HandlesMixedCasingAndExtraWhitespace()
    {
        // Arrange
        var raw = "   45   BLVD    Zerktouni,   FLR   3   ";

        // Act
        var normalized = _engine.NormalizeAddress(raw);

        // Assert
        normalized.Should().Be("45 boulevard zerktouni floor 3");
    }

    [Fact]
    public void NormalizeAddress_NullOrEmptyString_ReturnsEmpty()
    {
        // Act & Assert
        _engine.NormalizeAddress("").Should().BeEmpty();
        _engine.NormalizeAddress("   ").Should().BeEmpty();
    }

    [Fact]
    public void ComputeSoundex_PhoneticallyEquivalentVariants_ProduceIdenticalSoundex()
    {
        // Arrange & Act
        var soundex1 = _engine.ComputeSoundex("Smith");
        var soundex2 = _engine.ComputeSoundex("Smyth");
        var soundex3 = _engine.ComputeSoundex("Robert");
        var soundex4 = _engine.ComputeSoundex("Rupert");

        // Assert
        soundex1.Should().Be(soundex2);
        soundex3.Should().Be(soundex4);
    }

    [Fact]
    public void ComputeSoundex_EmptyOrNull_ReturnsDefault0000()
    {
        // Act & Assert
        _engine.ComputeSoundex("").Should().Be("0000");
        _engine.ComputeSoundex("   ").Should().Be("0000");
    }

    [Fact]
    public void ComputeLevenshteinDistance_ExactMatch_ReturnsZero()
    {
        // Act
        var dist = _engine.ComputeLevenshteinDistance("Casablanca", "casablanca");

        // Assert
        dist.Should().Be(0);
    }

    [Fact]
    public void ComputeLevenshteinDistance_SingleSubstitutionOrInsertion_ReturnsOne()
    {
        // Act
        var subDist = _engine.ComputeLevenshteinDistance("Casablanca", "Casablanka");
        var insDist = _engine.ComputeLevenshteinDistance("Casablanca", "Casablancaa");

        // Assert
        subDist.Should().Be(1);
        insDist.Should().Be(1);
    }

    [Fact]
    public void ComputeLevenshteinDistance_DistanceTwo_ReturnsTwo()
    {
        // Act
        var dist = _engine.ComputeLevenshteinDistance("Boulevard", "Bulevarde");

        // Assert
        dist.Should().Be(2);
    }

    [Fact]
    public void ResolveCanonicalAddress_ExactMatch_ReturnsExistingCanonicalId()
    {
        // Arrange
        var addr1 = "10 Boulevard Anfa, Casablanca";
        var id1 = _engine.ResolveCanonicalAddress(addr1);

        // Act
        var id2 = _engine.ResolveCanonicalAddress("10 Boulevard Anfa, Casablanca");

        // Assert
        id2.Should().Be(id1);
    }

    [Fact]
    public void ResolveCanonicalAddress_LevenshteinDistanceWithinTwo_FuzzyMatchesToSameCanonicalId()
    {
        // Arrange: "10 Boulevard Anfa Casablanca" vs typo "10 Boulevard Anfaa Casablanca" (Levenshtein = 1)
        var addr1 = "10 Boulevard Anfa, Casablanca";
        var id1 = _engine.ResolveCanonicalAddress(addr1);

        // Act
        var addr2 = "10 Boulevard Anfaa, Casablanca";
        var id2 = _engine.ResolveCanonicalAddress(addr2);

        // Assert
        id2.Should().Be(id1, "fuzzy matching within Levenshtein edit distance <= 2 must resolve to existing canonical address");
    }

    [Fact]
    public void ResolveCanonicalAddress_DistinctAddress_CreatesNewCanonicalId()
    {
        // Arrange
        var id1 = _engine.ResolveCanonicalAddress("10 Boulevard Anfa, Casablanca");

        // Act
        var id2 = _engine.ResolveCanonicalAddress("99 Avenue Hassan II, Rabat");

        // Assert
        id2.Should().NotBe(id1);
    }

    [Fact]
    public void LinkCustomerEntities_CompleteProfile_CreatesCustomerAndLinkedEntities()
    {
        // Arrange
        var request = new LinkCustomerEntitiesRequest
        {
            CustomerId = "cust_alpha_1",
            DeviceFingerprint = "dev_fp_111",
            PhysicalAddress = "123 Rue de Rome, Casablanca",
            PhoneNumber = "+212 600 123456",
            PaymentMethod = "4111 2222 3333 4444",
            BankRib = "1010 2020 3030 4040",
            InitialCustomerRiskScore = 25.0
        };

        // Act
        var response = _engine.LinkCustomerEntities(request);

        // Assert
        response.Should().NotBeNull();
        response.CustomerNodeId.Should().Be("customer:cust_alpha_1");
        response.EdgesCreated.Should().Be(5);
        response.LinkedNodeIds.Should().HaveCount(5);

        var custNode = _engine.GetNode("customer:cust_alpha_1");
        custNode.Should().NotBeNull();
        custNode!.RiskScore.Should().Be(25.0);
    }

    [Fact]
    public void LinkCustomerEntities_FuzzyAddressMatch_LinksMultipleCustomersToSameCanonicalAddress()
    {
        // Arrange: Cust1 uses "St", Cust2 uses "Street" with a minor typo
        var req1 = new LinkCustomerEntitiesRequest
        {
            CustomerId = "cust_link_1",
            PhysicalAddress = "12 Palm St, Marrakech"
        };
        var req2 = new LinkCustomerEntitiesRequest
        {
            CustomerId = "cust_link_2",
            PhysicalAddress = "12 Palm Street, Marrakesh" // edit distance Marrakech vs Marrakesh is 1
        };

        // Act
        var res1 = _engine.LinkCustomerEntities(req1);
        var res2 = _engine.LinkCustomerEntities(req2);

        // Assert
        var addr1 = res1.LinkedNodeIds[0];
        var addr2 = res2.LinkedNodeIds[0];
        addr2.Should().Be(addr1, "both addresses should resolve to the same canonical address node");

        var inEdges = _engine.GetInEdges(addr1);
        inEdges.Should().HaveCount(2);
        inEdges.Select(e => e.SourceNodeId).Should().Contain(["customer:cust_link_1", "customer:cust_link_2"]);
    }

    [Fact]
    public void DiscoverConnectedComponents_DisjointSubgraphs_PartitionsIntoSeparateComponents()
    {
        // Arrange: Subgraph A (cust_A1 -- dev_A) and Subgraph B (cust_B1 -- dev_B)
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_A1",
            TargetNodeId = "device:dev_A",
            RelationType = GraphRelationType.PlacedFromDevice
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:cust_B1",
            TargetNodeId = "device:dev_B",
            RelationType = GraphRelationType.PlacedFromDevice
        });

        // Act
        var components = _analyzer.DiscoverConnectedComponents();

        // Assert
        components.Should().HaveCount(2);
        components.Should().Contain(c => c.Contains("customer:cust_A1") && c.Contains("device:dev_A"));
        components.Should().Contain(c => c.Contains("customer:cust_B1") && c.Contains("device:dev_B"));
    }

    [Fact]
    public void DiscoverConnectedComponents_EmptyGraph_ReturnsEmpty()
    {
        // Arrange
        _engine.Clear();

        // Act
        var components = _analyzer.DiscoverConnectedComponents();

        // Assert
        components.Should().BeEmpty();
    }

    [Fact]
    public void CalculateModularity_DisjointCommunities_ReturnsPositiveModularity()
    {
        // Arrange
        _engine.AddEdge(new GraphEdge { SourceNodeId = "n1", TargetNodeId = "n2", RelationType = GraphRelationType.PlacedFromDevice });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "n3", TargetNodeId = "n4", RelationType = GraphRelationType.PlacedFromDevice });

        var communities = _analyzer.DiscoverConnectedComponents();

        // Act
        var modularity = _analyzer.CalculateModularity(communities);

        // Assert
        modularity.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void CalculateCentralityMetrics_DegreeCentrality_AccuratelyCountsNeighbors()
    {
        // Arrange: Hub device connected to 3 customers
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c1", TargetNodeId = "device:hub_dev", RelationType = GraphRelationType.PlacedFromDevice });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c2", TargetNodeId = "device:hub_dev", RelationType = GraphRelationType.PlacedFromDevice });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c3", TargetNodeId = "device:hub_dev", RelationType = GraphRelationType.PlacedFromDevice });

        // Act
        var metrics = _analyzer.CalculateCentralityMetrics();
        var hubMetric = metrics.FirstOrDefault(m => m.NodeId == "device:hub_dev");

        // Assert
        hubMetric.Should().NotBeNull();
        hubMetric!.Degree.Should().Be(3);
        hubMetric.LinkedCustomersCount.Should().Be(3);
    }

    [Fact]
    public void CalculateCentralityMetrics_PageRank_CalculatesValuesForNodes()
    {
        // Arrange
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:p1", TargetNodeId = "device:d1", RelationType = GraphRelationType.PlacedFromDevice });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:p2", TargetNodeId = "device:d1", RelationType = GraphRelationType.PlacedFromDevice });

        // Act
        var metrics = _analyzer.CalculateCentralityMetrics();

        // Assert
        metrics.Should().NotBeEmpty();
        metrics.All(m => m.PageRank > 0.0).Should().BeTrue();
    }

    [Fact]
    public void DetectMuleNodes_IdentifiesDeviceLinkedToMultipleCustomers()
    {
        // Arrange: 1 device linked to 3 distinct customer accounts
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c_mule1", TargetNodeId = "device:bot_emulator", RelationType = GraphRelationType.PlacedFromDevice });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c_mule2", TargetNodeId = "device:bot_emulator", RelationType = GraphRelationType.PlacedFromDevice });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c_mule3", TargetNodeId = "device:bot_emulator", RelationType = GraphRelationType.PlacedFromDevice });

        // Act
        var mules = _analyzer.DetectMuleNodes(minConnectedCustomers: 3);

        // Assert
        mules.Should().ContainSingle(id => id == "device:bot_emulator");
    }

    [Fact]
    public void DetectMuleNodes_IdentifiesBankRibSharedAcrossMultipleCustomers()
    {
        // Arrange: 1 Bank RIB linked to 4 customer accounts
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c_rib1", TargetNodeId = "rib:mule_account_77", RelationType = GraphRelationType.TransferredToBank });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c_rib2", TargetNodeId = "rib:mule_account_77", RelationType = GraphRelationType.TransferredToBank });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c_rib3", TargetNodeId = "rib:mule_account_77", RelationType = GraphRelationType.TransferredToBank });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:c_rib4", TargetNodeId = "rib:mule_account_77", RelationType = GraphRelationType.TransferredToBank });

        // Act
        var mules = _analyzer.DetectMuleNodes(minConnectedCustomers: 3);

        // Assert
        mules.Should().Contain("rib:mule_account_77");
    }

    [Fact]
    public void DetectFraudClusters_IdentifiesHighRiskRing_WhenBankRibAndDeviceAreShared()
    {
        // Arrange: 3 customers sharing 1 device and 1 bank RIB
        var req1 = new LinkCustomerEntitiesRequest
        {
            CustomerId = "ring_cust_1",
            DeviceFingerprint = "ring_device_x",
            BankRib = "RIB_FRAUD_999",
            InitialCustomerRiskScore = 60.0
        };
        var req2 = new LinkCustomerEntitiesRequest
        {
            CustomerId = "ring_cust_2",
            DeviceFingerprint = "ring_device_x",
            BankRib = "RIB_FRAUD_999",
            InitialCustomerRiskScore = 70.0
        };
        var req3 = new LinkCustomerEntitiesRequest
        {
            CustomerId = "ring_cust_3",
            DeviceFingerprint = "ring_device_x",
            BankRib = "RIB_FRAUD_999",
            InitialCustomerRiskScore = 65.0
        };

        _engine.LinkCustomerEntities(req1);
        _engine.LinkCustomerEntities(req2);
        _engine.LinkCustomerEntities(req3);

        // Act
        var clusters = _analyzer.DetectFraudClusters(minRiskScore: 50.0);

        // Assert
        clusters.Should().NotBeEmpty();
        var highRiskRing = clusters.First();
        highRiskRing.ClusterRiskScore.Should().BeGreaterThanOrEqualTo(75.0);
        highRiskRing.PrimaryRiskFactors.Should().Contain(f => f.Contains("CRITICAL_BANK_RIB_SHARING"));
        highRiskRing.PrimaryRiskFactors.Should().Contain(f => f.Contains("DEVICE_FINGERPRINT_COLLUSION"));
    }

    [Fact]
    public void DetectFraudClusters_SingleHonestCustomer_AssignsLowRiskScore()
    {
        // Arrange
        _engine.Clear();
        var req = new LinkCustomerEntitiesRequest
        {
            CustomerId = "honest_cust_1",
            DeviceFingerprint = "unique_device_clean",
            PhysicalAddress = "50 Rue Mozart, Casablanca",
            InitialCustomerRiskScore = 5.0
        };
        _engine.LinkCustomerEntities(req);

        // Act
        var clusters = _analyzer.DetectFraudClusters();

        // Assert
        clusters.Should().HaveCount(1);
        clusters[0].ClusterRiskScore.Should().BeLessThanOrEqualTo(25.0);
        clusters[0].PrimaryRiskFactors.Should().Contain("LOW_RISK_ISOLATED_IDENTITY");
    }

    [Fact]
    public void DetectFraudClusters_HighCodRefusalRate_ElevatesClusterRiskScore()
    {
        // Arrange: Customers with 85% COD refusal rate
        _engine.Clear();
        _engine.AddOrUpdateNode(new GraphNode
        {
            NodeId = "customer:bad_cod_1",
            NodeType = GraphNodeType.Customer,
            Properties = new Dictionary<string, string> { ["cod_refusal_rate"] = "0.85" }
        });
        _engine.AddOrUpdateNode(new GraphNode
        {
            NodeId = "customer:bad_cod_2",
            NodeType = GraphNodeType.Customer,
            Properties = new Dictionary<string, string> { ["cod_refusal_rate"] = "0.90" }
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:bad_cod_1",
            TargetNodeId = "device:shared_cod_dev",
            RelationType = GraphRelationType.PlacedFromDevice
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:bad_cod_2",
            TargetNodeId = "device:shared_cod_dev",
            RelationType = GraphRelationType.PlacedFromDevice
        });

        // Act
        var clusters = _analyzer.DetectFraudClusters();

        // Assert
        clusters.Should().NotBeEmpty();
        var cluster = clusters.First();
        cluster.PrimaryRiskFactors.Should().Contain(f => f.Contains("HIGH_COD_REFUSAL_RATE"));
    }

    [Fact]
    public async Task QuarantineClusterAsync_MarksAllMemberNodesAsQuarantined()
    {
        // Arrange
        _engine.Clear();
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:q1", TargetNodeId = "device:qd1", RelationType = GraphRelationType.PlacedFromDevice });
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:q2", TargetNodeId = "device:qd1", RelationType = GraphRelationType.PlacedFromDevice });

        var clusters = _analyzer.DetectFraudClusters();
        var targetClusterId = clusters.First().ClusterId;

        // Act
        var action = await _containment.QuarantineClusterAsync(targetClusterId, reason: "Syndicate Test Quarantine");

        // Assert
        action.QuarantinedNodesCount.Should().Be(3);
        _engine.GetNode("customer:q1")!.IsQuarantined.Should().BeTrue();
        _engine.GetNode("customer:q2")!.IsQuarantined.Should().BeTrue();
        _engine.GetNode("device:qd1")!.IsQuarantined.Should().BeTrue();
    }

    [Fact]
    public async Task QuarantineClusterAsync_FreezesOrdersAndEscrowAmount_AndCancelsWaybills()
    {
        // Arrange
        _engine.Clear();
        _engine.AddOrUpdateNode(new GraphNode
        {
            NodeId = "customer:ord_cust_1",
            NodeType = GraphNodeType.Customer,
            Properties = new Dictionary<string, string>
            {
                ["pending_orders"] = "3",
                ["escrow_balance"] = "1500.50"
            }
        });
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:ord_cust_1",
            TargetNodeId = "device:ord_dev",
            RelationType = GraphRelationType.PlacedFromDevice
        });

        var clusters = _analyzer.DetectFraudClusters();
        var clusterId = clusters.First().ClusterId;

        // Act
        var action = await _containment.QuarantineClusterAsync(clusterId);

        // Assert
        action.FrozenOrdersCount.Should().Be(3);
        action.FrozenEscrowAmount.Should().Be(1500.50m);
        action.CancelledWaybills.Should().HaveCount(3);
        action.CancelledWaybills.Should().Contain("WB-FRAUD-customer-ord_cust_1-ORD001");
    }

    [Fact]
    public async Task QuarantineClusterAsync_EmitsContainmentAuditEventToLedger()
    {
        // Arrange
        _engine.Clear();
        _engine.AddEdge(new GraphEdge { SourceNodeId = "customer:audit_c", TargetNodeId = "device:audit_d", RelationType = GraphRelationType.PlacedFromDevice });
        var clusters = _analyzer.DetectFraudClusters();
        var clusterId = clusters.First().ClusterId;

        // Act
        await _containment.QuarantineClusterAsync(clusterId, reason: "Red ring lockdown", operatorId: "OP_SECURITY_99");

        // Assert
        var trail = _containment.GetAuditTrail();
        trail.Should().NotBeEmpty();
        var audit = trail.Last();
        audit.ClusterId.Should().Be(clusterId);
        audit.Reason.Should().Be("Red ring lockdown");
        audit.OperatorId.Should().Be("OP_SECURITY_99");
        audit.ActionSummary.QuarantinedNodesCount.Should().Be(2);

        var retrievedByCluster = _containment.GetAuditEventByCluster(clusterId);
        retrievedByCluster.Should().NotBeNull();
        retrievedByCluster!.EventId.Should().Be(audit.EventId);
    }

    [Fact]
    public async Task QuarantineClusterAsync_NonExistentCluster_ReturnsZeroSummary()
    {
        // Act
        var action = await _containment.QuarantineClusterAsync("cluster_ghost_9999");

        // Assert
        action.QuarantinedNodesCount.Should().Be(0);
        action.FrozenOrdersCount.Should().Be(0);
        action.FrozenEscrowAmount.Should().Be(0m);
        action.CancelledWaybills.Should().BeEmpty();
    }

    [Fact]
    public void AddIdentityGraphFraudServices_RegistersServicesInServiceCollection_WithoutBuildingProvider()
    {
        // Arrange: Rule strictly requires: Do not call services.BuildServiceProvider() in tests.
        // Direct ServiceDescriptor assertion on ServiceCollection is required.
        var services = new ServiceCollection();

        // Act
        services.AddIdentityGraphFraudServices();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(IIdentityGraphEngine) &&
                                        sd.ImplementationType == typeof(IdentityGraphEngine) &&
                                        sd.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(sd => sd.ServiceType == typeof(IGraphClusterAnalyzerService) &&
                                        sd.ImplementationType == typeof(GraphClusterAnalyzerService) &&
                                        sd.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(sd => sd.ServiceType == typeof(IFraudRingContainmentService) &&
                                        sd.ImplementationType == typeof(FraudRingContainmentService) &&
                                        sd.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void GraphEngine_ThreadSafety_ConcurrentAddNodesAndEdges_ExecutesSafely()
    {
        // Arrange
        _engine.Clear();
        const int threadCount = 10;
        const int opsPerThread = 50;

        // Act
        Parallel.For(0, threadCount, t =>
        {
            for (var i = 0; i < opsPerThread; i++)
            {
                var custId = $"customer:concur_c_{t}_{i}";
                var devId = $"device:concur_dev_{t}";

                _engine.AddOrUpdateNode(new GraphNode
                {
                    NodeId = custId,
                    NodeType = GraphNodeType.Customer,
                    RiskScore = 10.0
                });

                _engine.AddEdge(new GraphEdge
                {
                    SourceNodeId = custId,
                    TargetNodeId = devId,
                    RelationType = GraphRelationType.PlacedFromDevice
                });
            }
        });

        // Assert
        _engine.NodeCount.Should().BeGreaterThanOrEqualTo(threadCount * opsPerThread);
        _engine.EdgeCount.Should().Be(threadCount * opsPerThread);
    }

    [Fact]
    public void GetNodeCentrality_ReturnsMetric_OrNullForUnknownNode()
    {
        // Arrange
        _engine.Clear();
        _engine.AddEdge(new GraphEdge
        {
            SourceNodeId = "customer:c_cent",
            TargetNodeId = "device:d_cent",
            RelationType = GraphRelationType.PlacedFromDevice
        });

        // Act
        var metric = _analyzer.GetNodeCentrality("device:d_cent");
        var nullMetric = _analyzer.GetNodeCentrality("non_existent_node");

        // Assert
        metric.Should().NotBeNull();
        metric!.NodeId.Should().Be("device:d_cent");
        nullMetric.Should().BeNull();
    }
}
