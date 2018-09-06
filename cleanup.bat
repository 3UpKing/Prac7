@echo off
rem cleans up all the bak and generated files so that you can
rem do a fresh build of the Parva Compiler.  Be careful!

del *.cod
del parva.cs
del parser.cs
del scanner.cs
del parva.exe
del errors
del listing.txt
del *.bak
del *.old
del parva\*.bak
