# 4. Push to GitHub and deploy to Azure

About 45 minutes the first time. Run every command from the repository root, in PowerShell.

## What it costs

Expect **about £0/$0 a month** at learning traffic.

| Part | Why it's free or nearly free |
|---|---|
| Container Apps | Scales to zero. The monthly free grant is about 200 hours of one running service at this size, shared across the subscription. |
| Application Insights | The first 5 GB a month per billing account is free, and each environment is capped at 0.1 GB a day. |
| GitHub Actions and GHCR | Free for public repositories and public images. |
| Alert rule | Off by default, because it can cost a little. |

Prices change, so check the [Azure calculator](https://azure.microsoft.com/pricing/calculator/). Step 2 sets a budget alert.

The Azure docs state the 5 GB allowance for logs. They don't say outright that it covers Application Insights data, so check **Cost Management** after a few days.

## Before you start

- [ ] An Azure subscription where you are **Owner**
- [ ] A GitHub account
- [ ] `git`, `gh` and `az` installed
- [ ] `dotnet test MicroShop.slnx` passes

## Step 1: Sign in

```powershell
az login
```

```powershell
gh auth login
```

Check that you're using the right subscription:

```powershell
az account show --output table
```

## Step 2: Set a budget alert

In the Azure portal: **Cost Management > Budgets > Add**. Set the amount to 5, with an email alert at 50%.

A budget **warns you but doesn't stop spending**. The real limits are scale to zero, one replica, and the daily log cap.

## Step 3: Push to GitHub

Create the local repository:

```powershell
git init -b main
```

```powershell
git add .
```

Check that nothing private is included, such as passwords or keys:

```powershell
git status
```

```powershell
git commit -m "Initial commit"
```

Create the GitHub repository and push. **Public** is needed because approval gates on private repositories require a paid plan, and the images are downloaded without credentials. Everything you commit will be visible to anyone.

```powershell
gh repo create micro-shop --public --source . --push
```

CI starts. Watch it (this can take a few minutes):

```powershell
gh run watch
```

**Expected:** CI passes. CD is skipped, because Azure isn't set up yet.

## Step 4: Make the images public

Azure downloads the images without a password, so they must be public.

1. On GitHub, open **your profile > Packages**.
2. For both `microshop-catalog` and `microshop-orders`: **Package settings > Change visibility > Public**. Skip any that are already public.

## Step 5: Connect GitHub to Azure

Read [`setup-azure.ps1`](../infra/setup-azure.ps1) first; it is commented. Change the repository, and the region if you like:

```powershell
./infra/setup-azure.ps1 -Repository "YOUR-GITHUB-NAME/micro-shop" -Location "uksouth"
```

If PowerShell blocks the script:

```powershell
powershell -ExecutionPolicy Bypass -File ./infra/setup-azure.ps1 -Repository "YOUR-GITHUB-NAME/micro-shop" -Location "uksouth"
```

**Check the result:** on GitHub, **Settings > Environments** shows `staging` and `production`, and production lists you as a required reviewer.

## Step 6: Deploy

Deploy the current commit:

```powershell
gh workflow run cd.yml -f commit=$(git rev-parse HEAD)
```

Or push any change to main, which starts CI and then CD.

1. Open **Actions > CD** on GitHub.
2. Staging deploys and is smoke tested. The first time takes several minutes.
3. Production shows **Review deployments**. Open the staging link, try it, then approve.

**Your URL** is shown on the run summary. Add `/scalar` for the API docs. The first request after a quiet period is slow, because the containers start from zero.

## Step 7: See the telemetry

Place some orders in Scalar, including products 98 and 99, and wait about five minutes.

In the portal, open `rg-microshop-production > appi-microshop-production`:

| Page | What to look for |
|---|---|
| **Application map** | Orders calling Catalog |
| **Failures** | The product 99 errors |
| **Performance** | The product 98 slowness |
| **Search** | Open one request to see the whole trace |

### Grafana dashboard

1. In the portal, open **Azure Monitor > Dashboards with Grafana > New > Import**.
2. Upload [`microshop-dashboard.json`](../infra/grafana/microshop-dashboard.json).
3. Paste the workspace resource ID into the box at the top:

```powershell
az monitor log-analytics workspace show --resource-group rg-microshop-production --workspace-name log-microshop-production --query id --output tsv
```

## Step 8: Practise

- **Release:** change a product price in `CatalogDbContext.cs` on a branch, open a pull request, merge it, and watch it reach production.
- **Broken build:** break a test in a pull request. The CI run fails.
- **Protect main:** **Settings > Rules > New branch ruleset**, then require a pull request and the CI checks.
- **Rollback:** **Actions > CD > Run workflow** with an older commit SHA.
- **Compare versions:** open `/version` on staging and production while production waits for approval.

## Delete everything

This permanently deletes both environments and their telemetry:

```powershell
az group delete --name rg-microshop-staging --yes --no-wait
```

```powershell
az group delete --name rg-microshop-production --yes --no-wait
```

Stop CD from running until you set it up again:

```powershell
gh variable delete AZURE_READY
```

## Troubleshooting

| Problem | Fix |
|---|---|
| `AADSTS700213` or "No matching federated identity record" | The error shows the subject GitHub sent. Compare it with the "Trusted subject" line from the setup script, and run the script again. It must match exactly, including upper and lower case. |
| "Authorization failed" just after setup | Azure permissions take a few minutes to spread. Re-run the job. |
| CD is skipped | `AZURE_READY` isn't set (step 5), or CI didn't pass. |
| "Find images" fails | CI hasn't published images for that commit. Only pushes to main publish. |
| `UNAUTHORIZED` or `ImagePullBackOff` | The images are still private (step 4). |
| Smoke test times out | Check the container logs, below. |
| No telemetry | Wait five minutes. Then check that the daily cap wasn't reached. |
| Region has no capacity | Choose another `-Location`, after deleting the resource groups. |

Container logs, for when an app doesn't start:

```powershell
az containerapp logs show --name ca-orders-staging --resource-group rg-microshop-staging --tail 50
```
