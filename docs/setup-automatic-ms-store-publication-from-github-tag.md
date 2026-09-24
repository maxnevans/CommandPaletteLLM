# Set up automatic Microsoft Store publication from a GitHub tag

This guide configures GitHub Actions to build and submit a new Command Palette
LLM version to Microsoft Store whenever a release tag such as `v1.2.3` is
pushed. The repository's
[`publish-store.yml`](../.github/workflows/publish-store.yml) workflow converts
that tag to MSIX version `1.2.3.0`, runs the tests, builds one x64/ARM64
`.msixupload` bundle, and submits it to Partner Center for certification.

Publication is not immediate. A successful workflow creates and commits the
Store submission, after which it must pass Microsoft Store certification.

> [!IMPORTANT]
> Microsoft currently documents automated app updates through the Store
> Developer CLI for free products. The product must already exist in Partner
> Center and must have been manually published at least once.

## Values required by the workflow

Create four GitHub repository secrets and one repository variable. Their names
must match this table exactly.

| GitHub name | Source value | GitHub type |
| --- | --- | --- |
| `AZURE_AD_TENANT_ID` | Microsoft Entra Directory (tenant) ID | Repository secret |
| `AZURE_AD_APPLICATION_CLIENT_ID` | Microsoft Entra Application (client) ID | Repository secret |
| `AZURE_AD_APPLICATION_SECRET` | Microsoft Entra client secret **Value** | Repository secret |
| `SELLER_ID` | Partner Center Seller ID | Repository secret |
| `STORE_PRODUCT_ID` | The app's 12-character Microsoft Store ID | Repository variable |

The tenant ID, client ID, and Store product ID are identifiers rather than
passwords. The workflow nevertheless expects the first two under GitHub's
`secrets` context. The client secret is sensitive and must never be committed
to this repository or printed in a workflow log.

## 1. Check the Partner Center product

1. Open [Microsoft Partner Center](https://partner.microsoft.com/dashboard).
2. Sign in with the account that manages Command Palette LLM.
3. Open the **Apps and games** workspace.
4. Select **Command Palette LLM**.
5. Confirm that the product has already been published and is live in
   Microsoft Store.
6. Confirm that it is a free product. Microsoft's current Store Developer CLI
   documentation does not support this update flow for paid products.
7. Confirm that the current `PRIVACY.md` text or its stable public URL is entered
   as the product's privacy policy.
8. Leave **Additional license terms** blank so Microsoft Store's Standard
   Application License Terms apply to end-user installations. Supplying custom
   terms replaces that default.
9. Confirm that the Store description clearly identifies the app as alpha or
   pre-release software, warns that updates may not preserve settings
   compatibility, and tells users to export a backup before updating.

Do not push a release tag until the remaining setup is complete. A tag push
immediately starts the publication workflow.

## 2. Check that Partner Center has an associated Entra tenant

1. In Partner Center, select the **Settings** gear in the upper-right corner.
2. Select **Account settings**.
3. In the left navigation, select **Tenants**.
4. Look for an associated Microsoft Entra tenant.

If the appropriate tenant is already present, note its name and continue to
the next section.

If no tenant is associated:

1. Select **Associate Microsoft Entra ID**.
2. Sign in with an account from the Entra tenant that should own the publishing
   application.
3. Review the organization and domain shown by Partner Center.
4. Select **Confirm**.
5. Return to **Account settings → Tenants**.
6. Confirm that the tenant is listed.

The application registration created in the next section must be in this same
tenant. If Partner Center offers **Create Microsoft Entra ID** instead, that
option can be used to create a tenant, but an existing organizational tenant is
usually preferable.

See Microsoft's guide to
[associating an Entra tenant with Partner Center](https://learn.microsoft.com/windows/apps/publish/partner-center/associate-existing-azure-ad-tenant-with-partner-center-account).

## 3. Create the Microsoft Entra publishing application

1. Open the [Microsoft Entra admin center](https://entra.microsoft.com/).
2. Sign in with an account that can create application registrations.
3. If the account can access multiple tenants, use the tenant/directory
   switcher near the upper-right corner to select the tenant associated with
   Partner Center in the previous section.
4. In the left navigation, select **Entra ID**.
5. Select **App registrations**.
6. Select **New registration**.
7. Enter the following values:
   - **Name:** `CommandPaletteLLM GitHub Publisher`
   - **Supported account types:** **Accounts in this organizational directory
     only — Single tenant**
   - **Redirect URI:** leave this blank
8. Select **Register**.

The registration is a service identity for GitHub Actions. Do not enter the
MSIX package identity, package publisher certificate name, or Store ID into
this form. No additional Microsoft Graph API permission is required for this
workflow; Partner Center grants the Store access in a later step.

See Microsoft's guide to
[registering a Microsoft Entra application](https://learn.microsoft.com/entra/identity-platform/quickstart-register-app).

## 4. Record the tenant ID and client ID

After registration, the application's **Overview** page opens. In the
**Essentials** section, copy these values to a temporary secure location:

1. Copy **Directory (tenant) ID**.
   - This will be stored in GitHub as `AZURE_AD_TENANT_ID`.
2. Copy **Application (client) ID**.
   - This will be stored in GitHub as
     `AZURE_AD_APPLICATION_CLIENT_ID`.

Do not copy **Object ID**. Object ID identifies the particular directory
object and will not work as the workflow's client ID.

## 5. Create the client secret

While still viewing the `CommandPaletteLLM GitHub Publisher` registration:

1. In the left navigation under **Manage**, select **Certificates & secrets**.
2. Select the **Client secrets** tab.
3. Select **New client secret**.
4. Enter these values:
   - **Description:** `GitHub Actions Microsoft Store publishing`
   - **Expires:** choose an appropriate lifetime, preferably 6 or 12 months
5. Select **Add**.
6. Immediately copy the new entry from the **Value** column.

The copied **Value** will be stored in GitHub as
`AZURE_AD_APPLICATION_SECRET`.

> [!WARNING]
> Copy the client secret **Value**, not its **Secret ID**. Entra displays the
> value only once. If it is lost, create a replacement secret. Do not put
> quotes around the value when saving it in GitHub.

Set a calendar reminder before the secret expires. To rotate it safely:

1. Create a new client secret without deleting the old one.
2. Replace `AZURE_AD_APPLICATION_SECRET` in GitHub with the new value.
3. Confirm that authentication or the next release succeeds.
4. Delete the old secret from Entra.

See Microsoft's guide to
[creating and managing client secrets](https://learn.microsoft.com/entra/msidweb/authentication/client-secrets).

## 6. Give the Entra application access to Partner Center

Creating the application in Entra does not grant it access to Store products.
Add it to the Partner Center account:

1. Return to [Partner Center](https://partner.microsoft.com/dashboard).
2. Select the **Settings** gear.
3. Select **Account settings**.
4. Select **User management**.
5. Open the **Microsoft Entra applications** tab. Some screens may still call
   this **Azure AD applications**.
6. Select **Add Microsoft Entra application**.
7. Select **Add Microsoft Entra application** or **Add existing application**.
8. Search for `CommandPaletteLLM GitHub Publisher`.
9. Select the application and then select **Next**.
10. Under **Roles applicable to developer programs**, select **Manager**.
11. Select **Add**.
12. Return to the Microsoft Entra applications list.
13. Confirm that `CommandPaletteLLM GitHub Publisher` appears with the
    **Manager** role.

If the application does not appear in the search results:

1. Confirm that it was created in the tenant associated with Partner Center.
2. Confirm that the current Partner Center account has the Manager role.
3. If required by the tenant, ask a Global Administrator to complete the
   operation.
4. Refresh the page; a new app registration can take a short time to appear.

The Manager role follows Microsoft's current Store GitHub Actions
instructions. See
[Manage Microsoft Entra applications in Partner Center](https://learn.microsoft.com/windows/apps/publish/partner-center/manage-azure-ad-applications-in-partner-center).

## 7. Find the Partner Center Seller ID

1. In Partner Center, select the **Settings** gear.
2. Select **Account settings**.
3. Open the **Developer** tab or select **Developer settings**.
4. Find the **Publisher IDs** section.
5. Copy the value labelled **Seller ID**.

Depending on the current Partner Center layout, the same value may appear
under **Account settings → Organization profile → Identifiers → Publisher**.

Store this value in GitHub as `SELLER_ID`. Do not substitute any of the
following:

- Partner ID
- User ID
- Publisher ID
- The `Package/Identity/Publisher` certificate name
- Microsoft Entra tenant ID

See Microsoft's description of
[Partner Center account identifiers](https://learn.microsoft.com/partner-center/account-settings/manage-account).

## 8. Find the Microsoft Store product ID

1. In Partner Center, open the **Apps and games** workspace.
2. Select **Command Palette LLM**.
3. In the product's left navigation, expand **Product management**.
4. Select **Product identity**.
5. Find **Store ID**.
6. Confirm that the 12-character identifier is `9PK5TNWKQ00Q` and copy it.

Store this value in GitHub as the repository variable `STORE_PRODUCT_ID`.

Do not use any of these other values from the Product identity page:

- Package/Identity/Name, such as `maxnevans.CommandPaletteLLM`
- Package Family Name
- Package SID
- Publisher ID
- A submission ID
- A SKU or availability ID containing `/`

See Microsoft's guide to
[viewing Store product identity details](https://learn.microsoft.com/windows/apps/publish/view-app-identity-details).

## 9. Add the four repository secrets to GitHub

1. Open the
   [CommandPaletteLLM repository on GitHub](https://github.com/maxnevans/CommandPaletteLLM).
2. Select the repository's **Settings** tab. If the tab is hidden, open the
   repository's overflow menu and select **Settings**.
3. In the left sidebar under **Security**, select **Secrets and variables**.
4. Select **Actions**.
5. Open the **Secrets** tab.
6. Select **New repository secret**.
7. Create each of the following secrets separately.

### Tenant ID

- **Name:** `AZURE_AD_TENANT_ID`
- **Secret:** paste the Entra **Directory (tenant) ID**
- Select **Add secret**.

### Client ID

- **Name:** `AZURE_AD_APPLICATION_CLIENT_ID`
- **Secret:** paste the Entra **Application (client) ID**
- Select **Add secret**.

### Client secret

- **Name:** `AZURE_AD_APPLICATION_SECRET`
- **Secret:** paste the client secret **Value**
- Select **Add secret**.

### Seller ID

- **Name:** `SELLER_ID`
- **Secret:** paste the Partner Center **Seller ID**
- Select **Add secret**.

GitHub does not display a saved secret's value later. It can only be replaced.
This is expected. See GitHub's guide to
[creating repository secrets](https://docs.github.com/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets).

## 10. Add the Store product ID as a GitHub variable

Remain on **Settings → Secrets and variables → Actions**, then:

1. Select the **Variables** tab.
2. Select **New repository variable**.
3. Enter:
   - **Name:** `STORE_PRODUCT_ID`
   - **Value:** the app's 12-character Store ID
4. Select **Add variable**.

This value must be a repository variable rather than a repository secret
because the workflow reads it through `${{ vars.STORE_PRODUCT_ID }}`.

## 11. Verify the configuration

On GitHub, **Settings → Secrets and variables → Actions → Secrets** must list:

```text
AZURE_AD_APPLICATION_CLIENT_ID
AZURE_AD_APPLICATION_SECRET
AZURE_AD_TENANT_ID
SELLER_ID
```

The **Variables** tab must list:

```text
STORE_PRODUCT_ID
```

Also verify all of the following before publishing:

- The Entra application was created in the tenant associated with Partner
  Center.
- `CommandPaletteLLM GitHub Publisher` appears under Partner Center's
  Microsoft Entra applications.
- The application has the Partner Center **Manager** role.
- The client secret has not expired.
- `STORE_PRODUCT_ID` is the app's 12-character Store ID.
- The product is already published and live.
- The product is free.
- The workflow and tagging script have been committed before the release tag
  is created.

## 12. Create the release tag locally

From PowerShell in the repository root, run one of these commands:

```powershell
# Increment patch: v1.2.3 -> v1.2.4
.\scripts\New-ReleaseTag.ps1

# Increment minor and reset patch: v1.2.3 -> v1.3.0
.\scripts\New-ReleaseTag.ps1 -Minor

# Increment major and reset minor and patch: v1.2.3 -> v2.0.0
.\scripts\New-ReleaseTag.ps1 -Major
```

[`New-ReleaseTag.ps1`](../scripts/New-ReleaseTag.ps1) reads the highest local
tag matching `vMajor.Minor.Patch`. If no release tag exists, it starts from the
version in `Directory.Build.props`. It creates an annotated tag on the current
commit but deliberately does not push it.

Review the new tag before publishing. Replace the example tag with the tag
printed by the script:

```powershell
git show v0.0.2
```

Confirm that the displayed commit contains the workflow, the tagging script,
and every change intended for the release.

## 13. Push the release tag

Push only the tag printed by the script:

```powershell
git push origin v0.0.2
```

Pushing the tag triggers the **Publish to Microsoft Store** GitHub Actions
workflow. Avoid `git push --tags` unless every local tag is intentionally being
published.

## 14. Monitor publication

1. Open the repository on GitHub.
2. Select **Actions**.
3. Select **Publish to Microsoft Store**.
4. Open the workflow run associated with the release tag.
5. Confirm that these stages succeed:
   - test execution
   - x64/ARM64 Store bundle creation
   - Partner Center authentication
   - Microsoft Store submission
6. Download the saved `.msixupload` workflow artifact if a copy of the exact
   submitted package is needed.
7. Open Partner Center and select **Command Palette LLM**.
8. Open the product submission/release page.
9. Confirm that the new submission and package version appear.
10. Monitor Microsoft Store certification until it completes.
11. After the certified release is live, verify its WinGet catalog entry:

    ```powershell
    winget show --id 9PK5TNWKQ00Q --source msstore
    ```

    Users can install the signed Store package through WinGet with:

    ```powershell
    winget install --id 9PK5TNWKQ00Q --source msstore
    ```

The Store package version has four components. For example, tag `v1.2.3`
produces package version `1.2.3.0`. The final component remains zero.

## Troubleshooting

### Authentication fails

Check the following:

- `AZURE_AD_APPLICATION_SECRET` contains the secret **Value**, not Secret ID.
- The secret has not expired.
- Tenant ID and client ID came from the same Entra application.
- The Entra application belongs to the tenant associated with Partner Center.
- The Entra application is present in Partner Center with the Manager role.
- The Seller ID belongs to the Partner Center publisher that owns the product.

### The product cannot be found

Confirm that `STORE_PRODUCT_ID` is the 12-character Store ID shown under
**Product management → Product identity**. Do not use Package/Identity/Name,
Package Family Name, SKU ID, submission ID, or an Entra application ID.

### GitHub Actions did not start

Confirm that:

- The tag was pushed to `origin`, not merely created locally.
- The tag has exactly the form `vMajor.Minor.Patch`, such as `v1.2.3`.
- The tagged commit contains `.github/workflows/publish-store.yml`.
- GitHub Actions are enabled for the repository.

List local tags with:

```powershell
git tag --list
```

List tags available from the remote with:

```powershell
git ls-remote --tags origin
```

### A wrong tag was created but not pushed

Delete only the local tag, then create the correct one:

```powershell
git tag --delete v1.2.3
.\scripts\New-ReleaseTag.ps1
```

If the tag was already pushed, do not delete or replace it casually: a pushed
release tag may already have started a Store submission. Inspect GitHub Actions
and Partner Center before taking corrective action.

## Official references

- [Publish app updates to Microsoft Store with GitHub Actions](https://learn.microsoft.com/windows/apps/publish/msstore-dev-cli/github-actions)
- [Microsoft Store Developer CLI for MSIX](https://learn.microsoft.com/windows/apps/publish/msstore-dev-cli/overview)
- [Manage Microsoft Entra applications in Partner Center](https://learn.microsoft.com/windows/apps/publish/partner-center/manage-azure-ad-applications-in-partner-center)
- [Associate an Entra tenant with Partner Center](https://learn.microsoft.com/windows/apps/publish/partner-center/associate-existing-azure-ad-tenant-with-partner-center-account)
- [Register a Microsoft Entra application](https://learn.microsoft.com/entra/identity-platform/quickstart-register-app)
- [Use client secrets](https://learn.microsoft.com/entra/msidweb/authentication/client-secrets)
- [View Microsoft Store product identity details](https://learn.microsoft.com/windows/apps/publish/view-app-identity-details)
- [Create GitHub Actions repository secrets](https://docs.github.com/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets)
