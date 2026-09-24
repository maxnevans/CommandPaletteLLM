# Publish a Microsoft Store and WinGet release

The package uses the Partner Center identity reserved for this product:

```text
Name:                 maxnevans.CommandPaletteLLM
Publisher:            CN=D8B7BDC7-1445-4AE6-BEEC-E9C2D0FD7ACD
PublisherDisplayName: maxnevans
```

Two workflows run when a tag named `vMajor.Minor.Patch` is pushed:

- The [Store publishing workflow](../.github/workflows/publish-store.yml) tests the tagged commit, maps the tag to the four-part MSIX version `Major.Minor.Patch.0`, builds one unsigned x64/ARM64 `.msixupload` bundle, saves it as a workflow artifact, and submits it to Partner Center for certification.
- The [WinGet installer workflow](../.github/workflows/publish-winget.yml) builds unpackaged x64 and ARM64 applications, creates self-signed metadata-only sparse packages for Command Palette discovery, wraps each result in an unsigned elevated Inno Setup installer, and attaches both installers to the matching GitHub Release.

## Configure publishing

Partner Center and the GitHub repository require one-time configuration before the Store workflow can publish. The WinGet workflow requires no signing account, paid certificate, or additional secret; it generates a temporary self-signed package certificate and deletes its private key after signing. Its installer requires elevation to trust the public certificate machine-wide.

See the [automatic Store publication guide](setup-automatic-ms-store-publication-from-github-tag.md) for the Store setup, and the [Command Palette WinGet publication guide](setup-winget-publication.md) for sparse identity registration, GitHub Releases, and the first community-manifest submission.

For Store publication:

1. Publish the product manually at least once and confirm that the package identity above exactly matches **Partner Center → Command Palette LLM → Product management → Product identity**.
2. Associate a Microsoft Entra application with the Partner Center account and grant it the **Manager** role.
3. Add `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, and `SELLER_ID` as GitHub Actions repository secrets.
4. Add the Partner Center product ID as a GitHub Actions repository variable named `STORE_PRODUCT_ID`.

Keep the privacy policy current in Partner Center. Leave **Additional license terms** blank so Microsoft Store's Standard Application License Terms apply to Store installations; supplying custom terms replaces that default.

## Create and publish a release tag

Create the next release tag locally with the PowerShell script:

```powershell
# Increment patch, for example v1.2.3 -> v1.2.4
.\scripts\New-ReleaseTag.ps1

# Increment minor and reset patch, for example v1.2.3 -> v1.3.0
.\scripts\New-ReleaseTag.ps1 -Minor

# Increment major and reset minor/patch, for example v1.2.3 -> v2.0.0
.\scripts\New-ReleaseTag.ps1 -Major
```

Run these commands from the repository root. The script creates an annotated tag on `HEAD` but does not push it. Review the tagged commit, then explicitly start publishing with the command printed by the script, such as `git push origin v1.2.4`. If no release tag exists yet, the script uses the version in [`Directory.Build.props`](../Directory.Build.props) as its starting point.

Before pushing a release tag, install and exercise a local x64 package and WinGet installer, verify sparse-package discovery and uninstall cleanup, test on ARM64 hardware when available, and run the current Windows App Certification Kit. Microsoft Store signs the submitted Store package after certification. The community installer remains unsigned. The `AppPackages`, `BundleArtifacts`, and `artifacts` directories are generated output and must remain uncommitted.

## Verify the published release

Once a certified release and community manifest are live, verify both sources:

```powershell
winget show --id 9PK5TNWKQ00Q --source msstore
winget show --id maxnevans.CommandPaletteLLM --source winget
```

The two identifiers represent separate distribution channels. The Store source retains Microsoft's opaque product ID and may report an unknown version; the community source provides the human-readable ID and version from its manifest. The Store package is signed by Microsoft. The community installer is unsigned and uses a self-signed metadata-only sparse package to make the extension discoverable.
