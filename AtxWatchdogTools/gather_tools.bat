@echo off

set build_type=%1
set output_path=%2

goto main
:__print_help
echo.
echo Usage:
echo        %0 ^<debug^|release^> [output_directory]
echo  Gathers all tools from the toolchain to a single output directory
echo  Based on the type of build specified. The solution must be already compiled
exit /B 1


:main
if [%build_type%]==[] (
    echo Error: Build Type not specified.
    goto __print_help
)

if /I NOT %build_type%==debug (
    if /I NOT %build_type%==release (
        echo Invalid build type. Options are 'debug' or 'release'
        exit /B 1
    )
)

if /I %build_type%==debug (
    set build_type=Debug
) else (
    set build_type=Release
)

if [%output_path%]==[] (
    set output_path=bin
    echo Using default path: %output_path%
) else (
    echo Using custom path: %output_path%
) 

if not exist %output_path%\ (
    mkdir %output_path%
)

if not exist %output_path%\%build_type%\ (
    mkdir %output_path%\%build_type%
)

echo Gathering tools...

echo atxdump...
copy /y "AtxDataDumper\bin\%build_type%\*" "%output_path%\%build_type%\" > nul
echo atxcsv...
copy /y "atxcsvdataconverter\bin\%build_type%\*" "%output_path%\%build_type%\" > nul
echo atxcsvplotter...
copy /y "atxcsvplotter\bin\%build_type%\*" "%output_path%\%build_type%\" > nul
echo atxcsvanalyzer...
copy /y "AtxCsvAnalyzer\bin\%build_type%\*" "%output_path%\%build_type%\" > nul

echo Complete.
exit /B 0