# PhotoForge Code Signing Guide (SignPath.io Free Open Source Program)

This guide documents the setup and automation of **free Authenticode code signing** for PhotoForge via the **SignPath Foundation**.

---

## 🌟 Why SignPath.io?

The [SignPath Foundation](https://about.signpath.io/open-source) provides **free Authenticode code signing certificates** from a Microsoft-trusted Certificate Authority for open-source projects.

### Benefits
- **Zero Cost ($0)**: Free for qualifying open source projects (PhotoForge is open-source under the MIT License).
- **Trusted by Microsoft**: Certificates chain up to the Microsoft Trusted Root Program.
- **SmartScreen Reputation**: Eliminates the blue *"Windows protected your PC"* untrusted publisher warning on Windows 10 & 11.
- **Automated CI/CD**: Integrates directly with GitHub Actions.

---

## 📋 Step 1: SignPath.io Account & Project Onboarding

1. **Sign Up for SignPath Open Source**:
   - Go to [**SignPath Open Source Program**](https://about.signpath.io/open-source).
   - Click **Apply for Free Open Source Signing**.
   - Authenticate with your GitHub account (`ramanacr`).

2. **Register the PhotoForge Project**:
   - Select repository: `https://github.com/ramanacr/photo-forge`.
   - License: `MIT License` (OSI-approved).
   - Confirm non-commercial open-source status.

3. **Configure Signing Policy**:
   - In the SignPath dashboard, create a **Signing Policy**:
     - **Signing Policy Name:** `test-signing` (for PRs) and `release-signing` (for official tags).
     - **Certificate:** `SignPath Foundation Code Signing CA`.
     - **Artifact Type:** `PE` / `ZIP` / `Setup.exe`.

4. **Generate API Token**:
   - Go to **Organization Settings** → **API Tokens** → **Create Token**.
   - Copy the **API Token** and your **Organization ID**.

---

## 🔑 Step 2: Configure GitHub Repository Secrets

Add the SignPath credentials to GitHub Secrets:

1. Navigate to: [**github.com/ramanacr/photo-forge/settings/secrets/actions**](https://github.com/ramanacr/photo-forge/settings/secrets/actions).
2. Add the following Repository Secrets:

| Secret Name | Description | Example / Value |
|---|---|---|
| `SIGNPATH_API_TOKEN` | API Token generated in SignPath dashboard | `sp_token_xxxxxxxxxxxx` |
| `SIGNPATH_ORGANIZATION_ID` | Your SignPath Organization GUID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `SIGNPATH_PROJECT_SLUG` | Project identifier configured in SignPath | `photo-forge` |
| `SIGNPATH_SIGNING_POLICY_SLUG` | Name of release signing policy | `release-signing` |

---

## 🤖 Step 3: Automated GitHub Actions Pipeline

PhotoForge's `.github/workflows/ci-release.yml` includes the SignPath step:

```yaml
- name: Submit Signing Request to SignPath
  if: startsWith(github.ref, 'refs/tags/v') && env.SIGNPATH_API_TOKEN != ''
  uses: SignPath/github-action-submit-signing-request@v1
  with:
    api-token: ${{ secrets.SIGNPATH_API_TOKEN }}
    organization-id: ${{ secrets.SIGNPATH_ORGANIZATION_ID }}
    project-slug: ${{ secrets.SIGNPATH_PROJECT_SLUG || 'photo-forge' }}
    signing-policy-slug: ${{ secrets.SIGNPATH_SIGNING_POLICY_SLUG || 'release-signing' }}
    github-token: ${{ secrets.GITHUB_TOKEN }}
    wait-for-completion: true
    output-artifact-directory: 'build/dist-signed'
    parameters: |
      description: 'PhotoForge Release ${{ github.ref_name }}'
      url: 'https://github.com/ramanacr/photo-forge'
```

---

## 🔄 Release Workflow Once SignPath is Active

1. Run the release command:
   ```powershell
   ./build/publish-release.ps1 -Bump patch
   ```
2. GitHub Actions builds the release assets.
3. SignPath receives the unsigned binary, signs it with the trusted CA certificate and timestamp authority, and returns the signed installer.
4. The signed binary is uploaded to GitHub Releases and deployed to GitHub Pages for Store consumption.

---

## 🔮 Future Option: Native MSIX Package

When transitioning to native Store packaging:
- Build `PhotoForge.Package.wapproj` or create an `.msixbundle`.
- Microsoft Store automatically signs MSIX packages upon upload at $0 cost without third-party certificate setups.
