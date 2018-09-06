
/* Driver for compilers and assemblers targetting the PVM - Coco/R for C#.
   P.D. Terry, Rhodes University, 2012
   revised 2015/08/03 */

//  ----------------------- you may need to change the "using" clauses:

using System;
using System.IO;
using System.Text;

namespace Parva {

  public class Parva {

    private static string newFileName(string s, string ext) {
      int i = s.LastIndexOf('.');
      if (i < 0) return s + ext; else return s.Substring(0, i) + ext;
    }

    public static void Main (string[] args) {
      bool mergeErrors = false, execution = true, immediate = false;
      string inputName = null;

      // ------------------------ process command line parameters:

      Console.WriteLine("Parva compiler 1.2016 September");

      for (int i = 0; i < args.Length; i++) {
        if      (args[i].ToLower().Equals("-l")) mergeErrors = true;
        else if (args[i].ToLower().Equals("-d")) Parser.debug = true;
        else if (args[i].ToLower().Equals("-n")) execution = false;
        else if (args[i].ToLower().Equals("-g")) immediate = true;
        else inputName = args[i];
      }
      if (inputName == null) {
        Console.WriteLine("No input file specified");
        Console.WriteLine("Usage: Parva [-l] [-d] [-n] [-g] source.pav");
        Console.WriteLine("-l directs source listing to listing.txt");
        Console.WriteLine("-d turns on debug mode");
        Console.WriteLine("-n no execution after compilation");
        Console.WriteLine("-g execute immediately after compilation (StdIn/StdOut)");
        System.Environment.Exit(1);
      }

      // ------------------------ parser and scanner initialization

      int pos = inputName.LastIndexOf('/');
      if (pos < 0) pos = inputName.LastIndexOf('\\');
      string dir = inputName.Substring(0, pos+1);

      Scanner.Init(inputName);
      Errors.Init(inputName, dir, mergeErrors);
      PVM.Init();
      Table.Init();

      // ------------------------ compilation

      Parser.Parse();
      Errors.Summarize();

      // ------------------------ interpretation

      bool assembledOK = Parser.Successful();
      int initSP = CodeGen.GetInitSP();
      string codeName = newFileName(inputName, ".cod");
      int codeLength = CodeGen.GetCodeLength();
      PVM.ListCode(codeName, codeLength);
      if (!assembledOK || codeLength == 0) {
        Console.WriteLine("Unable to interpret code");
        System.Environment.Exit(1);
      }
      else if (!execution) {
        Console.WriteLine("\nCompiled: exiting with no execution requested");
        System.Environment.Exit(1);
      }
      else {
        if (immediate) PVM.QuickInterpret(codeLength, initSP);
        char reply = 'n';
        do {
          Console.Write("\n\nInterpret [y/N]? ");
          reply = (Console.ReadLine() + " ").ToUpper()[0];
          if (reply == 'Y') PVM.Interpret(codeLength, initSP);
        } while (reply == 'Y');
      }
    } // main

  } // end Parva

} // end namespace
