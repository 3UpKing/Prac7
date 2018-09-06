@echo off
rem batch file for making a Coco/R C# application - CSC 301 2013
if "%1" == "" goto missing
coco %1.atg -options m %2 %3 %4 %5 %6
if errorlevel 1 goto badatg
rem del listing.txt
if not exist %1.cs goto stop
if not exist %1\*.cs goto short
csc %1.cs Parser.cs Scanner.cs Library.cs %1\*.cs > errors
goto check
:short
csc %1.cs Parser.cs Scanner.cs Library.cs > errors
:check
if errorlevel 1 goto errors
del errors
echo Compiled
goto stop
:errors
echo C# errors - see file "errors"
goto stop
:missing
echo no grammar file specified - use cmake ATGFile [options]
goto stop
:badatg
q listing.txt
:stop
