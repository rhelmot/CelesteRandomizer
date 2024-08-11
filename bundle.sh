#!/bin/bash -e

dotnet build
VERSION=$(cat Randomizer/everest.yaml | grep '^  Version' | cut -d' ' -f 4)
FILENAME=dist/Randomizer_${VERSION}${2}.zip
rm -f $FILENAME
mkdir -p dist
cd Randomizer/bin/${1-Debug}
zip -r ../../../${FILENAME} *
echo Finished in ${FILENAME}
