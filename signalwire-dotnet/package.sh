rm -rf *.nupkg
dotnet pack -c Release -o .
dotnet nuget push `ls *.nupkg` -k $NUGET_APIKEY -s https://api.nuget.org/v3/index.json
