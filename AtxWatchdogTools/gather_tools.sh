#!/bin/sh

build_type=$1
output_path=$2

function __print_help() {
    echo
    echo "Usage:"
    echo "       ${0} <debug|release> [output_directory]"
    echo " Gathers all tools from the toolchain to a single output directory"
    echo " Based on the type of build specified. The solution must be already compiled"
}

if [ -z $build_type ]; then
    echo "Error: Build Type not specified."
    __print_help
    exit 1
fi

if [ $build_type != "debug" ] && [ $build_type != "release" ]; then
    echo "Invalid build type. Options are 'debug' or 'release'"
    exit 1
fi

if [ $build_type == "debug" ]; then
    build_type="Debug"
else
    build_type="Release"
fi

if [ -z $output_path ]; then
    output_path="bin"
    echo "Using default path: ${output_path}"
else
    echo "Using custom path: ${output_path}"
fi

mkdir -p $output_path/$build_type

echo "Gathering tools..."

echo "atxdump..."
cp -fr AtxDataDumper/bin/${build_type}/* "${output_path}/${build_type}/"
echo "atxcsv..."
cp -fr atxcsvdataconverter/bin/${build_type}/* "${output_path}/${build_type}/"
echo "atxcsvplotter..."
cp -fr atxcsvplotter/bin/${build_type}/* "${output_path}/${build_type}/"
echo "atxcsvanalyzer..."
cp -fr AtxCsvAnalyzer/bin/${build_type}/* "${output_path}/${build_type}/"
echo "atxdfu..."
cp -fr AtxDfuTool/bin/${build_type}/* "${output_path}/${build_type}/"

echo "Complete."
exit 0