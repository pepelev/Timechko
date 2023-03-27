#!/bin/bash

rm -r bin docs
dotnet publish -c:Release
commit=$(git rev-parse HEAD)
git checkout publication
rm -r docs
cp bin/net7.0/browser-wasm/AppBundle/ docs/ -r
sed -i -E 's/<!--dev-->(.+)$/<!--dev \1 -->/g' docs/index.html
sed -i -E 's/<!--prod (.*) -->/\1/g'           docs/index.html
sed -i -E "s/https:\/\/github.com\/pepelev\/Timechko/https:\/\/github.com\/pepelev\/Timechko\/commit\/$commit/g" docs/index.html