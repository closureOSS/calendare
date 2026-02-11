# Development

Development on Windows with the NET SDK installed.

## Build Server

```shell
dotnet publish --os linux --arch x64 /t:PublishContainer /p:ContainerImageTags=`"$(nbgv get-version -v SemVer2)`;$(nbgv get-version -v MajorMinorVersion)`;$(nbgv get-version -v Version)`;latest`" -p ContainerRegistry=registry.example.com
```

The registry credentials need to be in ~/.docker/config.json.

## Build Database Migration

In the `Data` - subdirectory export the current database creation and migration script to `erm.sql` with

```shell
dotnet ef migrations script --idempotent --msbuildprojectextensionspath ..\artifacts\obj\Data\  --output erm.sql
```

if **actual changes** (i.e. a new migration) have occured. Don't change `erm.sql` if just external version numbers have changed to avoid confusion.

In Data subdirectory

```shell
dotnet ef migrations bundle --self-contained -r linux-x64 --force -o ../artifacts/efbundle
# OR for local execution on windows
dotnet ef migrations bundle --force -o ../artifacts/efbundle.exe
```

## Update OpenAPI specification

Start the server locally (or change the URL to a running instance)

```shell
curl -X GET  https://localhost:5443/openapi/v1/openapi.json >.\openapi.json
```

## Build Helm chart

```shell
helm registry login registry.example.com
```

While standing in the root directory (parent directory of subdirectory `deploy`) create and push helm package with

```shell
helm package --app-version "$(nbgv get-version -v SemVer2)" --version "$(nbgv get-version -v SemVer2)" .\deploy\
helm push .\calendare-server-$(nbgv get-version -v SemVer2).tgz oci://registry.example.com/helm
helm show values oci://registry.example.com/helm/calendare
```

The client libraries in other dependend repositories need to be updated.

## Development credentials

See [User secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0&tabs=windows) for background information.

Data and server have different `UserSecretsId`.

## Format of source code

Defined in `.editorconfig`.
