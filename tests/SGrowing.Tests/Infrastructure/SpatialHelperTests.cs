using SGrowing.Core.Spatial;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class SpatialHelperTests
{
    [Fact]
    public void CalculateHaversineKm_SameCoordinates_ReturnsZero()
    {
        var distance = PostGisSpatialHelper.CalculateHaversineKm(33.5731, -7.5898, 33.5731, -7.5898);
        Assert.Equal(0.0, distance);
    }

    [Fact]
    public void CalculateHaversineKm_CasablancaToRabat_ComputesAccurateDistance()
    {
        // Casablanca (33.5731, -7.5898) to Rabat (34.0209, -6.8416) ~ 86-88 km
        var distance = PostGisSpatialHelper.CalculateHaversineKm(33.5731, -7.5898, 34.0209, -6.8416);
        Assert.InRange(distance, 85.0, 90.0);
    }

    [Fact]
    public void CalculateHaversineKm_ParisToLondon_ComputesAccurateDistance()
    {
        // Paris (48.8566, 2.3522) to London (51.5074, -0.1278) ~ 343 km
        var distance = PostGisSpatialHelper.CalculateHaversineKm(48.8566, 2.3522, 51.5074, -0.1278);
        Assert.InRange(distance, 340.0, 346.0);
    }

    [Fact]
    public void CreateBoundingBoxEnvelope_StandardLocation_CalculatesValidBox()
    {
        var box = PostGisSpatialHelper.CreateBoundingBoxEnvelope(33.5731, -7.5898, 10.0);

        Assert.False(box.CrossesAntimeridian);
        Assert.True(box.MinLatitude < 33.5731);
        Assert.True(box.MaxLatitude > 33.5731);
        Assert.True(box.MinLongitude < -7.5898);
        Assert.True(box.MaxLongitude > -7.5898);
        Assert.InRange(box.MinLatitude, -90.0, 90.0);
        Assert.InRange(box.MaxLatitude, -90.0, 90.0);
    }

    [Fact]
    public void CreateBoundingBoxEnvelope_ZeroOrNegativeRadius_DefaultsToPositiveOneKm()
    {
        var boxZero = PostGisSpatialHelper.CreateBoundingBoxEnvelope(0.0, 0.0, 0.0);
        var boxOne = PostGisSpatialHelper.CreateBoundingBoxEnvelope(0.0, 0.0, 1.0);

        Assert.Equal(boxOne.MinLatitude, boxZero.MinLatitude);
        Assert.Equal(boxOne.MaxLatitude, boxZero.MaxLatitude);
        Assert.Equal(boxOne.MinLongitude, boxZero.MinLongitude);
        Assert.Equal(boxOne.MaxLongitude, boxZero.MaxLongitude);
    }

    [Fact]
    public void CreateBoundingBoxEnvelope_NearNorthPole_ClampsLatitudeToNinety()
    {
        var box = PostGisSpatialHelper.CreateBoundingBoxEnvelope(89.5, 0.0, 100.0);

        Assert.Equal(90.0, box.MaxLatitude);
        Assert.Equal(-180.0, box.MinLongitude);
        Assert.Equal(180.0, box.MaxLongitude);
    }

    [Fact]
    public void CreateBoundingBoxEnvelope_NearAntimeridian_FlagsCrossesAntimeridian()
    {
        // Longitude 179.9 with 50km radius should cross 180° antimeridian
        var box = PostGisSpatialHelper.CreateBoundingBoxEnvelope(0.0, 179.9, 50.0);

        Assert.True(box.CrossesAntimeridian);
    }

    [Fact]
    public void IsPointWithinBox_StandardBox_ReturnsCorrectContainment()
    {
        var box = PostGisSpatialHelper.CreateBoundingBoxEnvelope(33.5731, -7.5898, 5.0);

        // Center point is inside
        Assert.True(PostGisSpatialHelper.IsPointWithinBox(33.5731, -7.5898, box));

        // Far away point is outside
        Assert.False(PostGisSpatialHelper.IsPointWithinBox(48.8566, 2.3522, box));
    }

    [Fact]
    public void IsPointWithinBox_AntimeridianBox_EvaluatesWrapCorrectly()
    {
        var box = PostGisSpatialHelper.CreateBoundingBoxEnvelope(0.0, 179.9, 50.0);
        Assert.True(box.CrossesAntimeridian);

        // Point at 179.95 should be within
        Assert.True(PostGisSpatialHelper.IsPointWithinBox(0.0, 179.95, box));

        // Point on negative side of antimeridian (e.g. -179.95) should be within if wrap covered it
        Assert.True(PostGisSpatialHelper.IsPointWithinBox(0.0, -179.95, box));

        // Point far away (e.g. 0.0, 0.0) should be outside
        Assert.False(PostGisSpatialHelper.IsPointWithinBox(0.0, 0.0, box));
    }

    [Fact]
    public void BuildPostGisDWithinSql_GeneratesExpectedPostGisQuery()
    {
        var sql = PostGisSpatialHelper.BuildPostGisDWithinSql("location_geom", 33.5731, -7.5898, 5000.0);

        Assert.Contains("ST_DWithin(location_geom", sql);
        Assert.Contains("ST_MakePoint(-7.589800, 33.573100)", sql);
        Assert.Contains("4326)::geography", sql);
        Assert.Contains("5000.00", sql);
    }

    [Fact]
    public void BuildPostGisDistanceSql_GeneratesExpectedPostGisQuery()
    {
        var sql = PostGisSpatialHelper.BuildPostGisDistanceSql("location_geom", 33.5731, -7.5898);

        Assert.Contains("ST_Distance(location_geom", sql);
        Assert.Contains("ST_MakePoint(-7.589800, 33.573100)", sql);
        Assert.Contains("4326)::geography", sql);
    }
}
