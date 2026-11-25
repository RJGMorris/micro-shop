<#
.SYNOPSIS
    One-time setup that lets GitHub Actions deploy to Azure without any stored password.

.DESCRIPTION
    For each environment (staging and production) this creates:
      - a resource group
      - a managed identity that may change ONLY that resource group
      - a trust (federated credential) that lets ONLY that GitHub environment use the identity
      - the GitHub environment, limited to the main branch, with its settings
    Production also gets you as a required approver.

    Safe to run more than once. Before running: az login, gh auth login,
    and push the repository to GitHub.

.EXAMPLE
    ./infra/setup-azure.ps1 -Repository "your-name/micro-shop" -Location "uksouth"
#>
param(
    [Parameter(Mandatory)] [string] $Repository,
    [string] $Location = "uksouth"
)

$ErrorActionPreference = "Stop"

# Pipe plain text to gh. Some PowerShell setups add a byte-order mark, which breaks JSON.
$OutputEncoding = [System.Text.Encoding]::ASCII

# Command-line tools don't throw when they fail, so check after each step.
function Assert-Success([string] $Step) {
    if ($LASTEXITCODE -ne 0) { throw "Failed: $Step" }
}

# ---------- 1. Look up the subscription and repository ----------
$subscriptionId = az account show --query id --output tsv
Assert-Success "Read the Azure subscription (run 'az login' first)"
$tenantId = az account show --query tenantId --output tsv
Assert-Success "Read the Azure tenant"

$repo = gh api "repos/$Repository" | ConvertFrom-Json
Assert-Success "Read the GitHub repository (has it been pushed?)"
$oidc = gh api "repos/$Repository/actions/oidc/customization/sub" | ConvertFrom-Json
Assert-Success "Read the GitHub OIDC settings"
$approverId = gh api user --jq .id
Assert-Success "Read your GitHub user id"

# ---------- 2. Turn on the Azure services we use (free) ----------
foreach ($provider in "Microsoft.App", "Microsoft.OperationalInsights", "Microsoft.Insights", "Microsoft.ManagedIdentity") {
    Write-Host "Registering $provider ..."
    az provider register --namespace $provider --wait
    Assert-Success "Register $provider"
}

foreach ($environment in "staging", "production") {
    Write-Host "`n=== $environment ===" -ForegroundColor Cyan
    $resourceGroup = "rg-microshop-$environment"
    $identityName = "id-github-microshop-$environment"

    # ---------- 3. Resource group and identity ----------
    az group create --name $resourceGroup --location $Location --output none
    Assert-Success "Create resource group"

    az identity create --name $identityName --resource-group $resourceGroup --location $Location --output none
    Assert-Success "Create identity"

    $identity = az identity show --name $identityName --resource-group $resourceGroup --output json | ConvertFrom-Json
    Assert-Success "Read identity"

    # ---------- 4. Trust: which GitHub workflow may use this identity ----------
    # The subject must match GitHub's token exactly. New repositories include permanent
    # ids, so nobody can take over the trust by re-registering a deleted name. Older
    # tutorials show "repo:owner/name:...", which does not work for new repositories.
    if ($oidc.sub_claim_prefix) {
        $subject = "$($oidc.sub_claim_prefix):environment:$environment"
    }
    else {
        $subject = "repo:{0}@{1}/{2}@{3}:environment:{4}" -f `
            $repo.owner.login, $repo.owner.id, $repo.name, $repo.id, $environment
    }
    Write-Host "Trusted subject: $subject"

    az identity federated-credential create `
        --name "github" `
        --identity-name $identityName `
        --resource-group $resourceGroup `
        --issuer "https://token.actions.githubusercontent.com" `
        --subject $subject `
        --audiences "api://AzureADTokenExchange" `
        --output none
    Assert-Success "Create federated credential"

    # ---------- 5. Least privilege: access to this resource group only ----------
    az role assignment create `
        --assignee-object-id $identity.principalId `
        --assignee-principal-type ServicePrincipal `
        --role "Contributor" `
        --scope "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup" `
        --output none
    Assert-Success "Assign role"

    # ---------- 6. GitHub environment ----------
    $settings = @{
        # Only the branches listed below may deploy here.
        deployment_branch_policy = @{ protected_branches = $false; custom_branch_policies = $true }
    }
    if ($environment -eq "production") {
        $settings.reviewers = @(@{ type = "User"; id = [int] $approverId })
    }
    $settings | ConvertTo-Json -Depth 5 |
        gh api --method PUT "repos/$Repository/environments/$environment" --input - --silent
    Assert-Success "Create GitHub environment"

    $branches = gh api "repos/$Repository/environments/$environment/deployment-branch-policies" --jq ".branch_policies[].name"
    Assert-Success "Read branch policies"
    if ($branches -notcontains "main") {
        gh api --method POST "repos/$Repository/environments/$environment/deployment-branch-policies" -f name=main --silent
        Assert-Success "Allow deployments from main"
    }

    # These are identifiers, not secrets, so they are stored as variables.
    $variables = @{
        AZURE_CLIENT_ID       = $identity.clientId
        AZURE_TENANT_ID       = $tenantId
        AZURE_SUBSCRIPTION_ID = $subscriptionId
        AZURE_RESOURCE_GROUP  = $resourceGroup
    }
    foreach ($name in $variables.Keys) {
        gh variable set $name --env $environment --repo $Repository --body $variables[$name]
        Assert-Success "Set variable $name"
    }
}

# The last switch: until this is true, the CD workflow skips itself.
gh variable set AZURE_READY --repo $Repository --body "true"
Assert-Success "Set AZURE_READY"

Write-Host "`nDone. The next push to main will deploy to staging." -ForegroundColor Green
