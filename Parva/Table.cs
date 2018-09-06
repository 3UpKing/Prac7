// Handle symbol and label tables for Parva compiler (C# version)
// P.D. Terry, Rhodes University, 2016
// As supplied for the last practical of the course
// 2016/08/30

using Library;
using System.Collections.Generic;
using System.Text;
using System;

namespace Parva {

  class Types {
  // Identifier (and expression) types
    public const int
      noType    =  0,             // The numbering is significant, as
      nullType  =  2,             // array types are denoted by these
      intType   =  4,             // numbers + 1
      boolType  =  6,
      charType  =  8,
      voidType  = 10;

    static List<string> typeNames = new List<string>();

    static int nextType = 0;

    public static int AddType(string name) {
    // Generate (and return) next type id, and add to list of type names
      int thisType = nextType;
      nextType += 2;
      typeNames.Add(name);        // simple
      typeNames.Add(name + "[]"); // matching array type
      return thisType;
    } // Types.AddType

    public static string Name(int type) {
      return typeNames[type];
    } // Types.Name

    public static void Show(OutFile lst) {
    // For use in debugging
      foreach (string s in typeNames) lst.Write(s + " ");
      lst.WriteLine();
    } // Types.Show

  } // end Types

  class Kinds {
  // Identifier kinds
    public const int
      Con   = 0,
      Var   = 1,
      Fun   = 2;

    public static string[] kindNames = { "const", "var  ", "func "};

  } // end Kinds

  class Entry {
  // All fields initialized, but are modified after construction (by semantic analyser)
    public int     kind        = Kinds.Var;
    public string  name        = "";
    public int     type        = Types.noType;
    public int     value       = 0;       // constants
    public int     offset      = 0;       // variables
    public bool    declared    = true;    // true for all except sentinel entry
    public Entry   nextInScope = null;    // link to next entry in current scope
    public int     nParams     = 0;       // functions
    public Label   entryPoint  = new Label(false);
    public Entry   firstParam  = null;
  } // end Entry

  class StackFrame {
  // Objects of this type are used to keep track of the space needed by variables as
  // they are declared and come in and out of scope
    public int size = 0;
  } // end StackFrame

  class DesType {
  // Objects of this type are associated with l-value and r-value designators
    public Entry entry;           // the identifier properties
    public int type;              // designator type (not always the entry type)

    public DesType(Entry entry) {
      this.entry  = entry;
      this.type   = entry.type;   // may need modification later
    } // constructor

    public override string ToString() {
    // Returns string representation of this designator (for debuggng)
      return (entry.name + "        ").Substring(0, 8)
             + " type "     + (Types.Name(type) + "        ").Substring(0, 8)
             + " offset "   + entry.offset;
    } // ToString

  } // end DesType

  class ConstRec {
  // Objects of this type are associated with literal constants
    public int value;             // value of a constant literal
    public int type;              // constant type (determined from the literal)
  } // end ConstRec

  class Scope {
  // Handle scope rules in blocks and functions
    public Scope outer;           // link to enclosing (outer) scope
    public Entry firstInScope;    // link to first identifier entry in this scope
  } // end Scope

  class Table {
  // The table is held as a linked list of scope nodes each pointing to a linked list
  // of entries for the corresponding scopes.  Each list terminates with the dummy sentinel
  // entry to handle undeclared entries in a simple fashion

    static Scope topScope = null;
    static Entry sentinelEntry;   // marker node at end of each scope list

    public static void Insert(Entry entry) {
    // Adds entry to symbol table
      Entry earlierEntry = Find(entry.name);
      if (earlierEntry.declared)
        Parser.SemError("earlier declaration of " + entry.name + " is still in scope");
      sentinelEntry.name = entry.name;
      Entry look = topScope.firstInScope, previous = null;
      while (!look.name.Equals(entry.name)) {
        previous = look; look = look.nextInScope;
      }
      entry.nextInScope = look;
      if (previous == null) topScope.firstInScope = entry;
      else previous.nextInScope = entry;
      entry.declared = true;
    } // Table.Insert

    public static Entry Find(string name) {
    // Searches table for an entry matching name.  If found then return that
    // entry; otherwise return a sentinel entry (marked as not declared)
      sentinelEntry.name = name;
      Scope scope = topScope;
      while (scope != null) {
        Entry entry = scope.firstInScope;
        while (!entry.name.Equals(name)) entry = entry.nextInScope;
        if (entry != sentinelEntry) return entry;
        scope = scope.outer;
      }
      return sentinelEntry;
    } // Table.Find

    public static void OpenScope() {
    // Opens a scope record at the start of parsing a statement block
      Scope newScope = new Scope();
      newScope.outer = topScope;
      newScope.firstInScope = sentinelEntry;
      topScope = newScope;
    } // Table.OpenScope

    public static void CloseScope() {
    // Closes a scope record at the end of parsing a statement block
      topScope = topScope.outer;
    } // Table.CloseScope

    public static string Truncate(string str) {
    // Tuuncates str to 8 letters, space fill right if shorter
      return (str + "         ").Substring(0, 8);
    } // Table.Truncate

    public static void PrintTable(OutFile lst) {
    // Prints symbol table for diagnostic/debugging purposes
      lst.WriteLine();
      lst.WriteLine("Symbol table");
      Scope scope = topScope;
      while (scope != null) {
        Entry entry = scope.firstInScope;
        while (entry != sentinelEntry) {
          lst.Write(Truncate(entry.name) + " ");
          lst.Write(Truncate(Types.Name(entry.type)));
          switch (entry.kind) {
            case Kinds.Con:
              lst.Write(" Constant  ");
              lst.WriteLine(entry.value, 0);
              break;
            case Kinds.Var:
              lst.Write(" Variable ");
              lst.Write(entry.offset, 3);
              lst.WriteLine();
              break;
            case Kinds.Fun:
              lst.Write(" Function ");
              lst.Write(entry.entryPoint, 3);
              lst.WriteLine(entry.nParams, 3);
              break;
          }
          entry = entry.nextInScope;
        }
        scope = scope.outer;
      }
      lst.WriteLine();
    } // Table.PrintTable

    public static void Init() {
    // instigate standard simple types, clears table and sets up sentinel entry
      Types.AddType("none");
      Types.AddType("null");
      Types.AddType("int");
      Types.AddType("bool");
      Types.AddType("char");
      Types.AddType("void");
      sentinelEntry = new Entry();
      sentinelEntry.name = "";
      sentinelEntry.type = Types.noType;
      sentinelEntry.kind = Kinds.Var;
      sentinelEntry.nextInScope = null;
      sentinelEntry.declared = false;
      topScope = null;
      OpenScope();
    } // Table.Init

  } // end Table

} // namespace
