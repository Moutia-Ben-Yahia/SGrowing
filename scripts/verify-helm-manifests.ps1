<#
.SYNOPSIS
    Automated Helm Chart and GitOps Manifest Validation Suite for SGrowing.
.DESCRIPTION
    Validates Helm chart syntax, file integrity, template block balance, helper macros,
    container security contexts, zero-trust network policies, high-availability constructs,
    and ArgoCD GitOps application manifests.
.PARAMETER ChartPath
    Root directory of the SGrowing Helm chart.
.PARAMETER GitOpsPath
    Directory containing ArgoCD application CRD manifests.
.PARAMETER StrictSecurity
    Enforces strict restricted Pod Security Standards.
.EXAMPLE
    .\scripts\verify-helm-manifests.ps1
#>

[CmdletBinding()]
param(
    [string]$ChartPath = "",
    [string]$GitOpsPath = "",
    [switch]$StrictSecurity = $true
)

$ErrorActionPreference = "Continue"

# Resolve root directory robustly
$repoRoot = if ($PSScriptRoot) {
    Split-Path -Path $PSScriptRoot -Parent
} else {
    (Get-Location).Path
}

if (-not $ChartPath) {
    $ChartPath = Join-Path $repoRoot "deploy\helm\sgrowing"
}
if (-not $GitOpsPath) {
    $GitOpsPath = Join-Path $repoRoot "deploy\gitops\argocd"
}

$chartDir = [System.IO.Path]::GetFullPath($ChartPath)
$gitopsDir = [System.IO.Path]::GetFullPath($GitOpsPath)
$templatesDir = Join-Path $chartDir "templates"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "    SGrowing Cloud-Native Helm Chart and GitOps Audit Suite       " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "Chart Path : $chartDir" -ForegroundColor DarkGray
Write-Host "GitOps Path: $gitopsDir" -ForegroundColor DarkGray
Write-Host "Timestamp  : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')`n" -ForegroundColor DarkGray

$totalChecks = 0
$passedChecks = 0
$failedChecks = 0
$auditResults = [System.Collections.Generic.List[PSCustomObject]]::new()

function Record-Check {
    param(
        [string]$Category,
        [string]$TestName,
        [bool]$Success,
        [string]$Details = ""
    )

    $script:totalChecks++
    if ($Success) {
        $script:passedChecks++
        Write-Host "  [PASS] " -ForegroundColor Green -NoNewline
        Write-Host "$Category - $TestName" -NoNewline
        if ($Details) { Write-Host " ($Details)" -ForegroundColor DarkGray } else { Write-Host "" }
    } else {
        $script:failedChecks++
        Write-Host "  [FAIL] " -ForegroundColor Red -NoNewline
        Write-Host "$Category - $TestName" -NoNewline
        if ($Details) { Write-Host " - Error: $Details" -ForegroundColor Yellow } else { Write-Host "" }
    }

    $auditResults.Add([PSCustomObject]@{
        Category = $Category
        Test     = $TestName
        Passed   = $Success
        Details  = $Details
    })
}

# -------------------------------------------------------------------------
# Category 1: File Existence and Chart Layout
# -------------------------------------------------------------------------
Write-Host "`n1. File Integrity and Layout Verification" -ForegroundColor White

$expectedFiles = @(
    (Join-Path $chartDir "Chart.yaml"),
    (Join-Path $chartDir "values.yaml"),
    (Join-Path $chartDir "values.staging.yaml"),
    (Join-Path $chartDir "values.production.yaml"),
    (Join-Path $templatesDir "_helpers.tpl"),
    (Join-Path $templatesDir "deployment-api.yaml"),
    (Join-Path $templatesDir "deployment-worker.yaml"),
    (Join-Path $templatesDir "service-api.yaml"),
    (Join-Path $templatesDir "ingress.yaml"),
    (Join-Path $templatesDir "hpa.yaml"),
    (Join-Path $templatesDir "pdb.yaml"),
    (Join-Path $templatesDir "configmap.yaml"),
    (Join-Path $templatesDir "secrets.yaml"),
    (Join-Path $templatesDir "serviceaccount.yaml"),
    (Join-Path $templatesDir "networkpolicy.yaml"),
    (Join-Path $gitopsDir "application-staging.yaml"),
    (Join-Path $gitopsDir "application-production.yaml")
)

foreach ($filePath in $expectedFiles) {
    $exists = Test-Path -LiteralPath $filePath
    $rel = [System.IO.Path]::GetFileName($filePath)
    if ($filePath.Contains("templates")) { $rel = "templates/$rel" }
    elseif ($filePath.Contains("argocd")) { $rel = "argocd/$rel" }
    Record-Check "File Structure" "Manifest present: $rel" $exists
}

# -------------------------------------------------------------------------
# Category 2: Static YAML Syntax (via Python PyYAML Engine)
# -------------------------------------------------------------------------
Write-Host "`n2. Static YAML Syntax and Grammar Parsing" -ForegroundColor White

$cDirEscaped = $chartDir.Replace('\', '/')
$gDirEscaped = $gitopsDir.Replace('\', '/')

$pythonCmd = @"
import os, sys, yaml

files = [
    '$cDirEscaped/Chart.yaml',
    '$cDirEscaped/values.yaml',
    '$cDirEscaped/values.staging.yaml',
    '$cDirEscaped/values.production.yaml',
    '$gDirEscaped/application-staging.yaml',
    '$gDirEscaped/application-production.yaml'
]

errors = []
for f in files:
    try:
        with open(f, 'r', encoding='utf-8') as fp:
            doc = yaml.safe_load(fp)
            if not isinstance(doc, dict):
                errors.append(os.path.basename(f) + ' is not a valid YAML dictionary')
    except Exception as ex:
        errors.append(os.path.basename(f) + ' parsing error: ' + str(ex))

if errors:
    print('ERRORS:' + ';'.join(errors))
    sys.exit(1)
else:
    print('ALL_VALID')
    sys.exit(0)
"@

try {
    $pyYamlResult = python -c "$pythonCmd" 2>&1
    $pySuccess = ($LASTEXITCODE -eq 0) -and ($pyYamlResult -match "ALL_VALID")
    Record-Check "YAML Syntax" "Pure YAML files parse cleanly with PyYAML" $pySuccess "$pyYamlResult"
} catch {
    Record-Check "YAML Syntax" "Python PyYAML parser execution" $false $_.Exception.Message
}

# -------------------------------------------------------------------------
# Category 3: Chart.yaml Metadata Conformance
# -------------------------------------------------------------------------
Write-Host "`n3. Chart Metadata Conformance" -ForegroundColor White

$chartFile = Join-Path $chartDir "Chart.yaml"
if (Test-Path $chartFile) {
    $chartContent = Get-Content -Path $chartFile -Raw
    $hasV2 = $chartContent -match "apiVersion:\s*v2"
    $hasName = $chartContent -match "name:\s*sgrowing\b"
    $hasVersion = $chartContent -match "version:\s*1\.0\.0"
    $hasAppVersion = $chartContent -match "appVersion:\s*[`"']?1\.0\.0[`"']?"
    $hasDescription = $chartContent -match "description:\s*.*SGrowing"

    Record-Check "Chart Metadata" "apiVersion: v2" $hasV2
    Record-Check "Chart Metadata" "name: sgrowing" $hasName
    Record-Check "Chart Metadata" "version: 1.0.0" $hasVersion
    Record-Check "Chart Metadata" "appVersion: 1.0.0" $hasAppVersion
    Record-Check "Chart Metadata" "description defined" $hasDescription
} else {
    Record-Check "Chart Metadata" "Chart.yaml readable" $false "Chart.yaml missing"
}

# -------------------------------------------------------------------------
# Category 4: Helm Template Tag and Helper Balance Verification
# -------------------------------------------------------------------------
Write-Host "`n4. Helm Template Tag and Macro Syntax Validation" -ForegroundColor White

$helpersFile = Join-Path $templatesDir "_helpers.tpl"
if (Test-Path $helpersFile) {
    $helpersContent = Get-Content -Path $helpersFile -Raw
    $requiredMacros = @(
        "sgrowing.name",
        "sgrowing.fullname",
        "sgrowing.chart",
        "sgrowing.labels",
        "sgrowing.selectorLabels",
        "sgrowing.serviceAccountName"
    )

    foreach ($macro in $requiredMacros) {
        $defined = $helpersContent -match ('define "' + [regex]::Escape($macro) + '"')
        Record-Check "Helm Helpers" "Macro defined: $macro" $defined
    }
}

if (Test-Path $templatesDir) {
    $templateFiles = Get-ChildItem -Path $templatesDir -Filter "*.yaml"
    foreach ($tFile in $templateFiles) {
        $content = Get-Content -Path $tFile.FullName -Raw
        $opens = ([regex]::Matches($content, '\{\{-?\s*(?:if|range|with|define)\b')).Count
        $closes = ([regex]::Matches($content, '\{\{-?\s*end\b')).Count
        $balanced = ($opens -eq $closes)
        $detailStr = "$opens opens / $closes closes"
        Record-Check "Template Syntax" "$($tFile.Name) block tag balance" $balanced $detailStr
    }
}

# -------------------------------------------------------------------------
# Category 5: Security Context and Zero-Trust Hardening Audit
# -------------------------------------------------------------------------
Write-Host "`n5. Kubernetes Security and Restricted PSS Audit" -ForegroundColor White

$valuesFile = Join-Path $chartDir "values.yaml"
if (Test-Path $valuesFile) {
    $valuesContent = Get-Content -Path $valuesFile -Raw
    $runAsNonRoot = $valuesContent -match "runAsNonRoot:\s*true"
    $readOnlyRootFs = $valuesContent -match "readOnlyRootFilesystem:\s*true"
    $dropAllCaps = $valuesContent -match "drop:\s*\r?\n\s*-\s*ALL"
    $noPrivEscalation = $valuesContent -match "allowPrivilegeEscalation:\s*false"
    $seccompDefault = $valuesContent -match "type:\s*RuntimeDefault"

    Record-Check "Security Context" "runAsNonRoot: true" $runAsNonRoot
    Record-Check "Security Context" "readOnlyRootFilesystem: true" $readOnlyRootFs
    Record-Check "Security Context" "capabilities drop ALL" $dropAllCaps
    Record-Check "Security Context" "allowPrivilegeEscalation: false" $noPrivEscalation
    Record-Check "Security Context" "seccompProfile: RuntimeDefault" $seccompDefault
}

# Audit deployment-api.yaml for security mounts and health probes
$depApiFile = Join-Path $templatesDir "deployment-api.yaml"
if (Test-Path $depApiFile) {
    $apiContent = Get-Content -Path $depApiFile -Raw
    $hasTmpEmptyDir = $apiContent -match "name:\s*tmp-volume\s*\r?\n\s*emptyDir:"
    $hasLiveness = $apiContent -match "livenessProbe:" -and $apiContent -match "\.Values\.livenessProbe\.path"
    $hasReadiness = $apiContent -match "readinessProbe:" -and $apiContent -match "\.Values\.readinessProbe\.path"
    $hasOtelSidecar = $apiContent -match "\.Values\.otelCollector\.enabled"
    $hasChecksums = $apiContent -match "checksum/config" -and $apiContent -match "checksum/secret"

    Record-Check "API Deployment" "Writable /tmp emptyDir mounted for read-only root" $hasTmpEmptyDir
    Record-Check "API Deployment" "LivenessProbe configured" $hasLiveness
    Record-Check "API Deployment" "ReadinessProbe configured" $hasReadiness
    Record-Check "API Deployment" "OpenTelemetry Collector sidecar support" $hasOtelSidecar
    Record-Check "API Deployment" "Config and Secret sha256 rolling update checksums" $hasChecksums
}

# Audit NetworkPolicy
$netPolFile = Join-Path $templatesDir "networkpolicy.yaml"
if (Test-Path $netPolFile) {
    $npContent = Get-Content -Path $netPolFile -Raw
    $hasPolicyTypes = $npContent -match "policyTypes:\s*\r?\n\s*-\s*Ingress\s*\r?\n\s*-\s*Egress"
    $hasDnsEgress = $npContent -match "port:\s*53"
    $hasDbEgress = $npContent -match "\.Values\.postgresql\.port"
    $hasRedisEgress = $npContent -match "\.Values\.redis\.port"
    $hasHttpsEgress = $npContent -match "port:\s*443"

    Record-Check "Zero-Trust NetworkPolicy" "Strict Ingress and Egress policyTypes enabled" $hasPolicyTypes
    Record-Check "Zero-Trust NetworkPolicy" "DNS egress allowed (port 53)" $hasDnsEgress
    Record-Check "Zero-Trust NetworkPolicy" "PostgreSQL egress allowed" $hasDbEgress
    Record-Check "Zero-Trust NetworkPolicy" "Redis egress allowed" $hasRedisEgress
    Record-Check "Zero-Trust NetworkPolicy" "Outbound HTTPS (port 443) allowed" $hasHttpsEgress
}

# -------------------------------------------------------------------------
# Category 6: High Availability and Scaling Audit
# -------------------------------------------------------------------------
Write-Host "`n6. High Availability and Scaling Conformance" -ForegroundColor White

$prodFile = Join-Path $chartDir "values.production.yaml"
if (Test-Path $prodFile) {
    $prodContent = Get-Content -Path $prodFile -Raw
    $prodReplicas = $prodContent -match "replicaCount:\s*3"
    $prodHpa = $prodContent -match "enabled:\s*true" -and $prodContent -match "minReplicas:\s*3" -and $prodContent -match "maxReplicas:\s*10"
    $prodCpuTarget = $prodContent -match "targetCPUUtilizationPercentage:\s*75"
    $prodMemTarget = $prodContent -match "targetMemoryUtilizationPercentage:\s*80"
    $prodPdb = $prodContent -match "podDisruptionBudget:\s*\r?\n\s*enabled:\s*true\s*\r?\n\s*minAvailable:\s*2"
    $prodTls = $prodContent -match "tls:\s*\r?\n\s*-\s*secretName:\s*sgrowing-api-tls\s*\r?\n\s*hosts:\s*\r?\n\s*-\s*api\.sgrowing\.io"

    Record-Check "Production HA" "Baseline replicaCount: 3" $prodReplicas
    Record-Check "Production HA" "HPA enabled (min 3, max 10)" $prodHpa
    Record-Check "Production HA" "HPA CPU target: 75%" $prodCpuTarget
    Record-Check "Production HA" "HPA Memory target: 80%" $prodMemTarget
    Record-Check "Production HA" "PodDisruptionBudget minAvailable: 2" $prodPdb
    Record-Check "Production HA" "Ingress TLS configured for api.sgrowing.io" $prodTls
}

$stgFile = Join-Path $chartDir "values.staging.yaml"
if (Test-Path $stgFile) {
    $stgContent = Get-Content -Path $stgFile -Raw
    $stgReplicas = $stgContent -match "replicaCount:\s*1"
    $stgHost = $stgContent -match "host:\s*staging-api\.sgrowing\.internal"

    Record-Check "Staging Overrides" "Staging replicaCount: 1" $stgReplicas
    Record-Check "Staging Overrides" "Staging hostname: staging-api.sgrowing.internal" $stgHost
}

# -------------------------------------------------------------------------
# Category 7: GitOps ArgoCD Manifest Conformance
# -------------------------------------------------------------------------
Write-Host "`n7. GitOps ArgoCD Manifest Verification" -ForegroundColor White

$argoProd = Join-Path $gitopsDir "application-production.yaml"
if (Test-Path $argoProd) {
    $argoProdContent = Get-Content -Path $argoProd -Raw
    $isApp = $argoProdContent -match "kind:\s*Application"
    $hasSelfHeal = $argoProdContent -match "selfHeal:\s*true"
    $hasPrune = $argoProdContent -match "prune:\s*true"
    $hasProdVal = $argoProdContent -match "values\.production\.yaml"
    $hasProdNs = $argoProdContent -match "namespace:\s*sgrowing-production"

    Record-Check "ArgoCD GitOps" "Production Application CRD configured" $isApp
    Record-Check "ArgoCD GitOps" "Production automated self-heal enabled" $hasSelfHeal
    Record-Check "ArgoCD GitOps" "Production automated prune enabled" $hasPrune
    Record-Check "ArgoCD GitOps" "Production references values.production.yaml" $hasProdVal
    Record-Check "ArgoCD GitOps" "Production targets namespace sgrowing-production" $hasProdNs
}

$argoStg = Join-Path $gitopsDir "application-staging.yaml"
if (Test-Path $argoStg) {
    $argoStgContent = Get-Content -Path $argoStg -Raw
    $hasStgVal = $argoStgContent -match "values\.staging\.yaml"
    $hasStgNs = $argoStgContent -match "namespace:\s*sgrowing-staging"

    Record-Check "ArgoCD GitOps" "Staging references values.staging.yaml" $hasStgVal
    Record-Check "ArgoCD GitOps" "Staging targets namespace sgrowing-staging" $hasStgNs
}

# -------------------------------------------------------------------------
# Final Summary and Exit Code
# -------------------------------------------------------------------------
Write-Host "`n==================================================================" -ForegroundColor Cyan
Write-Host "                      AUDIT SUMMARY RESULTS                       " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "Total Verification Checks : $totalChecks"
Write-Host "Passed Checks             : $passedChecks" -ForegroundColor Green
Write-Host "Failed Checks             : $failedChecks" -ForegroundColor $(if ($failedChecks -eq 0) { "Green" } else { "Red" })

if ($failedChecks -eq 0) {
    Write-Host "`n[SUCCESS] All SGrowing Helm Charts and GitOps manifests are production-ready!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n[FAILURE] $failedChecks audit checks failed. Correct the errors above." -ForegroundColor Red
    exit 1
}
