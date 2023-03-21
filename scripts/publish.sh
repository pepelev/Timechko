rm -r bin docs
dotnet publish -c:Release
git checkout publication
rm -r docs
cp bin/net7.0/browser-wasm/AppBundle/ docs/ -r