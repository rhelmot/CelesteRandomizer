#!/bin/sh

msbuild
VERSION=$(cat Randomizer/everest.yaml | grep '^  Version' | cut -d' ' -f 4)
FILENAME=dist/Randomizer_${VERSION}${2}.zip
rm -f $FILENAME
cd Randomizer/bin/${1-Debug}
zip -r ../../../${FILENAME} *
echo Finished in ${FILENAME}
