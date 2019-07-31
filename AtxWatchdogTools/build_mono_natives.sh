#!/bin/sh

set -o pipefail
mkbundle=$(command -v mkbundle)

if [ ! -f $mkbundle ] || [ -z $mkbundle ]; then
    echo "Error: Mono bundler (mkbundle) cannot be found."
    exit 1
fi

libc_lib=""
libc_lib_local_workaround=0
if [[ $OSTYPE == darwin* ]]; then
    libc_lib="libc.dylib"

    if [ ! -f $libc_lib ]; then
        echo "NOTE: Generating native images for MacOS X requires libc.dylib"
        echo "Warning: Library ${libc_lib} not found in local directory. Trying /usr/lib"
        libc_lib="/usr/lib/${libc_lib}"
        libc_lib_local_workaround=1


        if [ ! -f $libc_lib ]; then
            echo "Error: Required library libc.dylib cannot be found."
            exit 1
        fi
    fi
fi

if [[ $OSTYPE == linux-gnu* ]]; then
    libc_lib="libc.so"

    if [ ! -f $libc_lib ]; then
        echo "NOTE: Generating native images for Linux requires libc.so"
        echo "Warning: Library ${libc_lib} not found in local directory. Trying /usr/lib"
        libc_lib="/usr/lib/${libc_lib}"

        if [ ! -f $libc_lib ]; then
            echo "Error: Required library libc.so cannot be found."
            exit 1
        fi
    fi
fi

if [ $libc_lib_local_workaround -eq 1 ]; then
    echo "Copying libc library to current directory as a workaround for an issue on some mono installations"
    $(cp -n $libc_lib ./)
fi

function __print_help() {
    echo
    echo "Usage:"
    echo "       ${0} <bin_path> <debug|release> <target_os>"
    echo " Generated native images from the specified path for Debug or Release assemblies"
    echo " Target OS specifies the target system for the built images."
    echo "   Options can be: osx, debian, ubuntu, raspbian"
}

function __gen_image() {
    clr_target=$1
    source="${2}/${3}"
    source_path=$2
    source_name=$3
    output=$4
    lib=${!5}
    lib_string=""

    if [ ! -z "$lib" ]; then
        read -a lib_values <<< "$lib"
        for val in "${lib_values[@]}"; do
            lib_string="--library $val ${lib_string} "
        done
    fi

    echo "Calling: -o ${output} -L ${source_path} --cross ${clr_target} --simple ${source} ${lib_string}"
    $mkbundle -o ${output} -L ${source_path} --cross ${clr_target} --simple ${source} ${lib_string}
}

function __trim_str() {
    str=$1
    $(echo $str | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')
    func_result=$str
}

bin_path=$1
build_type=$2
target_os=$3

if [ -z $bin_path ]; then
    echo "Error: Binaries path not specified."
    __print_help
    exit 1
fi

if [ -z $build_type ]; then
    echo "Error: Build Type not specified."
    __print_help
    exit 1
fi

if [ -z $target_os ]; then
    echo "Error: Target OS not specified."
    __print_help
    exit 1
fi

if [ $build_type != "debug" ] && [ $build_type != "release" ]; then
    echo "Error: Invalid build type. Options are 'debug' or 'release'"
    exit 1
fi

clr_target=$(${mkbundle} --local-targets | grep "${target_os}" | tail -n1)

if [ ! $? -eq 0 ]; then
    echo "Failed to execute mkbundle tool. Check mono installation and try again."
    exit 1
fi

__trim_str $clr_target
clr_target=$func_result

if [ -z $clr_target ]; then
    echo "There is no CLR target installed for the specified platform. Trying to install it..."
    sel_clr=$(${mkbundle} --list-targets | grep "${target_os}" | tail -n1)
    echo "Downloading and installing ${sel_clr} bundle..."
    $mkbundle --fetch-target ${sel_clr}

    if [ ! $? -eq 0 ]; then
        echo "Error: install failed."
        exit 1
    fi

    echo "CLR Installation complete."
    clr_target=$(${mkbundle} --local-targets | grep "${target_os}" | tail -n1)
    __trim_str $clr_target
    clr_target=$func_result

    if [ -z $clr_target ]; then
        echo "CLR image selection error. Cannot find the target CLR to use when bundling."
        exit 1
    fi
fi
target_path="native_images/${target_os}/${clr_target}/${build_type}"

echo "CLR Target: ${clr_target}"

mkdir -p $target_path

echo "Generating images..."

echo "atxdump..."
__gen_image $clr_target "${bin_path}/${build_type}" "atxdump.exe" "$target_path/atxdump"
if [ ! $? -eq 0 ]; then
    echo "Error: Failed to generate image."
    exit 1
fi

echo "atxcsv..."
__gen_image $clr_target "${bin_path}/${build_type}" "atxcsv.exe" "$target_path/atxcsv"
if [ ! $? -eq 0 ]; then
    echo "Error: Failed to generate image."
    exit 1
fi

echo "atxcsvplotter..."
if [[ $OSTYPE == darwin* ]]; then
    libs="${libc_lib}"
    __gen_image $clr_target "${bin_path}/${build_type}" "atxcsvplot.exe" "$target_path/atxcsvplot" libs
else
    __gen_image $clr_target "${bin_path}/${build_type}" "atxcsvplot.exe" "$target_path/atxcsvplot"
fi
if [ ! $? -eq 0 ]; then
    echo "Error: Failed to generate image."
    exit 1
fi

echo "atxcsvanalyzer..."
if [[ $OSTYPE == darwin* ]]; then
    libs="${libc_lib}"
    __gen_image $clr_target "${bin_path}/${build_type}" "atxcsvanalyze.exe" "$target_path/atxcsvanalyze" libs
else
    __gen_image $clr_target "${bin_path}/${build_type}" "atxcsvanalyze.exe" "$target_path/atxcsvanalyze"
fi
if [ ! $? -eq 0 ]; then
    echo "Error: Failed to generate image."
    exit 1
fi

if [ $libc_lib_local_workaround -eq 1 ]; then
    $(rm -f libc.dylib)
    $(rm -f libc.so)
fi

echo "Image generation complete."
exit 0