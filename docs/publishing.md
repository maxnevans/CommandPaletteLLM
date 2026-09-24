# Publish a Microsoft Store and WinGet release

The package uses the Partner Center identity reserved for this product:

```text
Name:                 maxnevans.CommandPaletteLLM
Publisher:            CN=D8B7BDC7-1445-4AE6-BEEC-E9C2D0FD7ACD
PublisherDisplayName: maxnevans
```

The [Store publishing workflow](../.github/workflows/publish-store.yml) runs when a tag named `vMajor.Minor.Patch` is pushed. It tests the tagged commit, maps the tag to the four-part MSIX version `Major.Minor.Patch.0`, builds one unsigned x64/ARM64 `.msixupload` bundle, saves that bundle as a workflow artifact, and submits it to Partner Center for certification.

## Configure publishing

Partner Center and the GitHub repository require one-time configuration before the workflow can publish. See the [step-by-step automatic Store publication guide](setup-automatic-ms-store-publication-from-github-tag.md) for the complete setup, verification, release, and troubleshooting procedure:

1. Publish the product manually at least once and confirm that the package identity above exactly matches **Partner Center → Command Palette LLM → Product management → Product identity**.
2. Associate a Microsoft Entra application with the Partner Center account and grant it the **Manager** role.
3. Add `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, and `SELLER_ID` as GitHub Actions repository secrets.
4. Add the Partner Center product ID as a GitHub Actions repository variable named `STORE_PRODUCT_ID`.

Keep the privacy policy current in Partner Center. Leave **Additional license terms** blank so Microsoft Store's Standard Application License Terms apply to end-user installations; supplying custom terms replaces that default. Store distribution is the only maintained end-user installation path for this project.

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

Before pushing a release tag, install and exercise a local x64 package, test on ARM64 hardware when available, and run the current Windows App Certification Kit. Microsoft Store signs the submitted package and makes it available only after certification completes. The `AppPackages` and `BundleArtifacts` directories are generated output and must remain uncommitted.

## Verify the published release

Once a certified release is live, verify that WinGet can resolve the Store product and reports the new version:

```powershell
winget show --id 9PK5TNWKQ00Q --source msstore
```

Store distribution, including WinGet's `msstore` source, is the only maintained end-user installation path. The repository does not produce or support a private, self-signed, or unsigned installer. A separate manifest in the WinGet community source is intentionally not used because it would require hosting and maintaining another signed installer channel.
