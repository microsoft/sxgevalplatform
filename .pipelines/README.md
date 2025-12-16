# Solution Build Pipeline

This folder contains Azure DevOps pipelines for building Dataverse solutions with ESRP code signing support.

## Pipelines

### `build-solution.yml`

Main pipeline for building and packaging Dataverse solutions. Creates importable solution ZIP files with optional ESRP certificate signing.

#### Parameters

| Parameter | Description | Default | Values |
|-----------|-------------|---------|--------|
| `solutionName` | The solution to build from `src/solutions/` | `dev` | `dev` (add more as needed) |
| `debug` | Enable debug output for troubleshooting | `false` | `true`, `false` |
| `isOfficialBuild` | Enable ESRP code signing for official builds | `false` | `true`, `false` |
| `solutionType` | Type of solution export | `Unmanaged` | `Managed`, `Unmanaged`, `Both` |

#### Usage

1. Navigate to Azure DevOps Pipelines
2. Create a new pipeline pointing to `.pipelines/build-solution.yml`
3. Run the pipeline and select the solution you want to build from the dropdown
4. For official releases, enable `isOfficialBuild` to sign plugin assemblies

## Templates

### `templates/build-solution-steps.yml`

Reusable steps for building plugins and packaging solutions.

### `templates/esrp-signing-steps.yml`

Reusable steps for ESRP code signing of DLL assemblies.

### `templates/validate-solution-steps.yml`

Reusable steps for validating solution package structure and contents.

## Variables

### `variables/solution-build-variables.yml`

Common variables used across solution build pipelines.

## ESRP Signing Configuration

For official builds, you need to configure the following in your Azure DevOps project:

1. **Service Connection**: Create an ESRP Code Signing service connection named `ESRP CodeSigning`

2. **Variable Groups**: Create a variable group with the following secrets:
   - `EsrpClientId` - ESRP App Registration Client ID
   - `EsrpTenantId` - ESRP App Registration Tenant ID
   - `EsrpKeyVaultName` - Azure Key Vault name containing ESRP certificates
   - `EsrpAuthCertName` - Authentication certificate name
   - `EsrpSignCertName` - Signing certificate name

3. **Key Vault**: Ensure your Key Vault contains:
   - Authentication certificate for ESRP
   - Signing certificate (Microsoft400 or equivalent)

## Adding New Solutions

To add a new solution to the pipeline:

1. Create your solution folder under `src/solutions/<solution-name>/`
2. Ensure it contains the standard Dataverse solution structure:
   ```
   <solution-name>/
   ├── Other/
   │   ├── Solution.xml
   │   └── Customizations.xml
   ├── PluginAssemblies/
   ├── Entities/
   ├── Workflows/
   └── ...
   ```
3. Edit `.pipelines/build-solution.yml` and add your solution name to the `solutionName` parameter values:
   ```yaml
   parameters:
     - name: solutionName
       displayName: 'Solution to Build'
       type: string
       default: 'dev'
       values:
         - 'dev'
         - '<your-new-solution>'  # Add here
   ```

## Output Artifacts

The pipeline produces the following artifacts:

- `SolutionArtifacts_<solutionName>/`
  - `<solutionName>_<version>.zip` - Solution package ready for import
  - `Plugins/` - Built plugin DLL files

For signed builds:
  - `<solutionName>_<version>_signed.zip` - Signed solution package

## Troubleshooting

### Debug Mode

Enable `debug: true` parameter to get detailed logging including:
- Environment information
- .NET SDK versions
- File copy operations
- Solution packaging details

### Common Issues

1. **Solution.xml not found**: Ensure your solution has the correct folder structure with `Other/Solution.xml`

2. **Plugin build failures**: Check that the plugin project compiles successfully locally first

3. **ESRP signing failures**: Verify service connection and Key Vault access permissions

4. **Version format issues**: Solution versions must follow `X.X.X.X` format
