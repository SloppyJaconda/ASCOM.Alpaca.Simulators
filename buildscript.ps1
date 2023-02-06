# dotnet build ./ASCOM.Alpaca.Simulators.sln
dotnet publish ./ASCOM.Alpaca.Simulators/ASCOM.Alpaca.Simulators.csproj -o ./foobar
# pushd ASCOM.Alpaca.Simulators/bin/Debug/net6.0
# rm ./publish.zip
# Compress-Archive ./publish -DestinationPath publish.zip
# # popd

# --use-current-runtime                Use current runtime as the target runtime.
# -o, --output <OUTPUT_DIR>            The output directory to place the published artifacts in.
# --manifest <MANIFEST>                The path to a target manifest file that contains the list of packages to be
#                                      excluded from the publish step.
# --no-build                           Do not build the project before publishing. Implies --no-restore.
# --sc, --self-contained               Publish the .NET runtime with your application so the runtime doesn't need to be
#                                      installed on the target machine.
#                                      The default is 'true' if a runtime identifier is specified.
# --no-self-contained                  Publish your application as a framework dependent application. A compatible .NET
#                                      runtime must be installed on the target machine to run your application.
# --nologo                             Do not display the startup banner or the copyright message.
# -f, --framework <FRAMEWORK>          The target framework to publish for. The target framework has to be specified in
#                                      the project file.
# -r, --runtime <RUNTIME_IDENTIFIER>   The target runtime to publish for. This is used when creating a self-contained
#                                      deployment.
#                                      The default is to publish a framework-dependent application.
# -c, --configuration <CONFIGURATION>  The configuration to publish for. The default is 'Debug'. Use the
#                                      `PublishRelease` property to make 'Release' the default for this command.
# --version-suffix <VERSION_SUFFIX>    Set the value of the $(VersionSuffix) property to use when building the project.
# --interactive                        Allows the command to stop and wait for user input or action (for example to
#                                      complete authentication).
# --no-restore                         Do not restore the project before building.
# -v, --verbosity <LEVEL>              Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal],
#                                      n[ormal], d[etailed], and diag[nostic].
# -a, --arch <arch>                    The target architecture.
# --os <os>                            The target operating system.
# --disable-build-servers              Force the command to ignore any persistent build servers.
# -?, -h, --help                       Show command line help.