using Library;
using System.Collections.Generic;



using System;
using System.IO;
using System.Text;

namespace Parva {

public class Parser {
	public const int _EOF = 0;
	public const int _identifier = 1;
	public const int _number = 2;
	public const int _stringLit = 3;
	public const int _charLit = 4;
	// terminals
	public const int EOF_SYM = 0;
	public const int identifier_Sym = 1;
	public const int number_Sym = 2;
	public const int stringLit_Sym = 3;
	public const int charLit_Sym = 4;
	public const int void_Sym = 5;
	public const int lparen_Sym = 6;
	public const int rparen_Sym = 7;
	public const int comma_Sym = 8;
	public const int lbrace_Sym = 9;
	public const int rbrace_Sym = 10;
	public const int semicolon_Sym = 11;
	public const int const_Sym = 12;
	public const int true_Sym = 13;
	public const int false_Sym = 14;
	public const int null_Sym = 15;
	public const int lbrackrbrack_Sym = 16;
	public const int int_Sym = 17;
	public const int bool_Sym = 18;
	public const int char_Sym = 19;
	public const int lbrack_Sym = 20;
	public const int rbrack_Sym = 21;
	public const int if_Sym = 22;
	public const int while_Sym = 23;
	public const int switch_Sym = 24;
	public const int default_Sym = 25;
	public const int colon_Sym = 26;
	public const int case_Sym = 27;
	public const int plus_Sym = 28;
	public const int minus_Sym = 29;
	public const int halt_Sym = 30;
	public const int return_Sym = 31;
	public const int read_Sym = 32;
	public const int write_Sym = 33;
	public const int barbar_Sym = 34;
	public const int andand_Sym = 35;
	public const int bang_Sym = 36;
	public const int new_Sym = 37;
	public const int star_Sym = 38;
	public const int slash_Sym = 39;
	public const int percent_Sym = 40;
	public const int equalequal_Sym = 41;
	public const int bangequal_Sym = 42;
	public const int less_Sym = 43;
	public const int lessequal_Sym = 44;
	public const int greater_Sym = 45;
	public const int greaterequal_Sym = 46;
	public const int equal_Sym = 47;
	public const int NOT_SYM = 48;
	// pragmas
	public const int CodeOn_Sym = 49;
	public const int CodeOff_Sym = 50;
	public const int DebugOn_Sym = 51;
	public const int DebugOff_Sym = 52;
	public const int StackDump_Sym = 53;
	public const int HeapDump_Sym = 54;
	public const int TableDump_Sym = 55;

	public const int maxT = 48;
	public const int _CodeOn = 49;
	public const int _CodeOff = 50;
	public const int _DebugOn = 51;
	public const int _DebugOff = 52;
	public const int _StackDump = 53;
	public const int _HeapDump = 54;
	public const int _TableDump = 55;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;

	public static Token token;    // last recognized token   /* pdt */
	public static Token la;       // lookahead token
	static int errDist = minErrDist;

	public static bool       // set or unsed by pragmas or command line arguments
    debug    = false,
    optimize = false,
    listCode = false,
    warnings = true;

  const bool
    known = true;

  // This next method might better be located in the code generator.  Traditionally
  // it has been left in the ATG file, but that might change in future years
  //
  // Not that while sequences like \n \r and \t result in special mappings to lf, cr and tab
  // other sequences like \x \: and \9 simply map to x, ; and 9 .  Most students don't seem
  // to know this!

  static string Unescape(string s) {
  /* Replaces escape sequences in s by their Unicode values */
    StringBuilder buf = new StringBuilder();
    int i = 0;
    while (i < s.Length) {
      if (s[i] == '\\') {
        switch (s[i+1]) {
          case '\\': buf.Append('\\'); break;
          case '\'': buf.Append('\''); break;
          case '\"': buf.Append('\"'); break;
          case  'r': buf.Append('\r'); break;
          case  'n': buf.Append('\n'); break;
          case  't': buf.Append('\t'); break;
          case  'b': buf.Append('\b'); break;
          case  'f': buf.Append('\f'); break;
          default:   buf.Append(s[i+1]); break;
        }
        i += 2;
      }
      else {
        buf.Append(s[i]);
        i++;
      }
    }
    return buf.ToString();
  } // Unescape

  // the following is global for expediency (fewer parameters needed)

  static Label mainEntryPoint = new Label(!known);

  static bool IsArith(int type) {
    return type == Types.intType || type == Types.charType || type == Types.noType;
  } // IsArith

  static bool IsBool(int type) {
    return type == Types.boolType || type == Types.noType;
  } // IsBool

  static bool IsArray(int type) {
    return (type % 2) == 1;
  } // IsArray

  static bool Compatible(int typeOne, int typeTwo) {
  // Returns true if typeOne is compatible (and comparable for equality) with typeTwo
    return    typeOne == typeTwo
           || IsArith(typeOne) && IsArith(typeTwo)
           || typeOne == Types.noType || typeTwo == Types.noType
           || IsArray(typeOne) && typeTwo == Types.nullType
           || IsArray(typeTwo) && typeOne == Types.nullType;
  } // Compatible

  static bool Assignable(int typeOne, int typeTwo) {
  // Returns true if a variable of typeOne may be assigned a value of typeTwo
    return    typeOne == typeTwo
           || typeOne == Types.intType && typeTwo == Types.charType
           || typeOne == Types.noType || typeTwo == Types.noType
           || IsArray(typeOne) && typeTwo == Types.nullType;
  } // Assignable

  static bool IsCall(out DesType des) {
    Entry entry = Table.Find(la.val);
    des = new DesType(entry);
    return entry.kind == Kinds.Fun;
  } // IsCall

/* -------------------------------------------------------------------------- */



	static void SynErr (int n) {
		if (errDist >= minErrDist) Errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public static void SemErr (string msg) {
		if (errDist >= minErrDist) Errors.Error(token.line, token.col, msg); /* pdt */
		errDist = 0;
	}

	public static void SemError (string msg) {
		if (errDist >= minErrDist) Errors.Error(token.line, token.col, msg); /* pdt */
		errDist = 0;
	}

	public static void Warning (string msg) { /* pdt */
		if (errDist >= minErrDist) Errors.Warn(token.line, token.col, msg);
		errDist = 2; //++ 2009/11/04
	}

	public static bool Successful() { /* pdt */
		return Errors.count == 0;
	}

	public static string LexString() { /* pdt */
		return token.val;
	}

	public static string LookAheadString() { /* pdt */
		return la.val;
	}

	static void Get () {
		for (;;) {
			token = la; /* pdt */
			la = Scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }
				if (la.kind == CodeOn_Sym) {
				listCode  = true;
				}
				if (la.kind == CodeOff_Sym) {
				listCode  = false;
				}
				if (la.kind == DebugOn_Sym) {
				debug     = true;
				}
				if (la.kind == DebugOff_Sym) {
				debug     = false;
				}
				if (la.kind == StackDump_Sym) {
				CodeGen.Stack();
				}
				if (la.kind == HeapDump_Sym) {
				CodeGen.Heap();
				}
				if (la.kind == TableDump_Sym) {
				Table.PrintTable(OutFile.StdOut);
				}

			la = token; /* pdt */
		}
	}

	static void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}

	static bool StartOf (int s) {
		return set[s, la.kind];
	}

	static void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}

	static bool WeakSeparator (int n, int syFol, int repFol) {
		bool[] s = new bool[maxT+1];
		if (la.kind == n) { Get(); return true; }
		else if (StartOf(repFol)) return false;
		else {
			for (int i=0; i <= maxT; i++) {
				s[i] = set[syFol, i] || set[repFol, i] || set[0, i];
			}
			SynErr(n);
			while (!s[la.kind]) Get();
			return StartOf(syFol);
		}
	}

	static void Parva() {
		CodeGen.FrameHeader();         // no arguments
		CodeGen.Call(mainEntryPoint);  // forward, incomplete
		CodeGen.LeaveProgram();
		while (la.kind == void_Sym) {
			FuncDeclaration();
		}
		Expect(EOF_SYM);
		if (!mainEntryPoint.IsDefined())
		  SemError("missing Main function");
	}

	static void FuncDeclaration() {
		StackFrame frame = new StackFrame();
		Entry function = new Entry();
		Expect(void_Sym);
		Ident(out function.name);
		function.kind = Kinds.Fun;
		function.type = Types.voidType;
		function.nParams = 0;
		function.firstParam = null;
		function.entryPoint = new Label(known);
		Table.Insert(function);
		Table.OpenScope();
		Expect(lparen_Sym);
		FormalParameters(function);
		Expect(rparen_Sym);
		frame.size = CodeGen.headerSize + function.nParams;
		if (function.name.ToUpper().Equals("MAIN")
		    && !mainEntryPoint.IsDefined()
		    && function.nParams == 0) {
		  mainEntryPoint.Here();
		}
		Body(frame);
		Table.CloseScope();
	}

	static void Ident(out string name) {
		Expect(identifier_Sym);
		name = token.val;
	}

	static void FormalParameters(Entry func) {
		Entry param;
		if (la.kind == int_Sym || la.kind == bool_Sym || la.kind == char_Sym) {
			OneParam(out param, func);
			func.firstParam = param;
			while (WeakSeparator(comma_Sym, 1, 2)) {
				OneParam(out param, func);
			}
		}
	}

	static void Body(StackFrame frame) {
		Label DSPLabel = new Label(known);
		int sizeMark = frame.size;
		CodeGen.OpenStackFrame(0);
		Expect(lbrace_Sym);
		while (StartOf(3)) {
			Statement(frame);
		}
		ExpectWeak(rbrace_Sym, 4);
		CodeGen.FixDSP(DSPLabel.Address(), frame.size - sizeMark);
		CodeGen.LeaveVoidFunction();
	}

	static void OneParam(out Entry param, Entry func) {
		param = new Entry();
		param.kind = Kinds.Var;
		param.offset = CodeGen.headerSize + func.nParams;
		func.nParams++;
		Type(out param.type);
		Ident(out param.name);
		Table.Insert(param);
	}

	static void Type(out int type) {
		BasicType(out type);
		if (la.kind == lbrackrbrack_Sym) {
			Get();
			type++;
		}
	}

	static void Statement(StackFrame frame) {
		while (!(StartOf(5))) {SynErr(49); Get();}
		switch (la.kind) {
		case lbrace_Sym: {
			Block(frame);
			break;
		}
		case const_Sym: {
			ConstDeclarations();
			break;
		}
		case int_Sym: case bool_Sym: case char_Sym: {
			VarDeclarations(frame);
			break;
		}
		case identifier_Sym: {
			AssignmentOrCall();
			break;
		}
		case if_Sym: {
			IfStatement(frame);
			break;
		}
		case while_Sym: {
			WhileStatement(frame);
			break;
		}
		case halt_Sym: {
			HaltStatement();
			break;
		}
		case return_Sym: {
			ReturnStatement();
			break;
		}
		case read_Sym: {
			ReadStatement();
			break;
		}
		case write_Sym: {
			WriteStatement();
			break;
		}
		case switch_Sym: {
			SwitchStatement(frame);
			break;
		}
		case semicolon_Sym: {
			Get();
			break;
		}
		default: SynErr(50); break;
		}
	}

	static void Block(StackFrame frame) {
		Table.OpenScope();
		Expect(lbrace_Sym);
		while (StartOf(3)) {
			Statement(frame);
		}
		ExpectWeak(rbrace_Sym, 6);
		Table.CloseScope();
	}

	static void ConstDeclarations() {
		Expect(const_Sym);
		OneConst();
		while (WeakSeparator(comma_Sym, 7, 8)) {
			OneConst();
		}
		ExpectWeak(semicolon_Sym, 6);
	}

	static void VarDeclarations(StackFrame frame) {
		int type;
		Type(out type);
		VarList(frame, type);
		ExpectWeak(semicolon_Sym, 6);
	}

	static void AssignmentOrCall() {
		int expType;
		DesType des;
		if (IsCall(out des)) {
			Expect(identifier_Sym);
			CodeGen.FrameHeader();
			Expect(lparen_Sym);
			Arguments(des);
			Expect(rparen_Sym);
			CodeGen.Call(des.entry.entryPoint);
		} else if (la.kind == identifier_Sym) {
			Designator(out des);
			if (des.entry.kind != Kinds.Var)
			  SemError("cannot assign to " + Kinds.kindNames[des.entry.kind]);
			AssignOp();
			Expression(out expType);
			if (!Assignable(des.type, expType))
			SemError("incompatible types in assignment");
			CodeGen.Assign(des.type);
		} else SynErr(51);
		ExpectWeak(semicolon_Sym, 6);
	}

	static void IfStatement(StackFrame frame) {
		Label falseLabel = new Label(!known);
		Expect(if_Sym);
		Expect(lparen_Sym);
		Condition();
		Expect(rparen_Sym);
		CodeGen.BranchFalse(falseLabel);
		Statement(frame);
		falseLabel.Here();
	}

	static void WhileStatement(StackFrame frame) {
		Label loopExit  = new Label(!known);
		Label loopStart = new Label(known);
		Expect(while_Sym);
		Expect(lparen_Sym);
		Condition();
		Expect(rparen_Sym);
		CodeGen.BranchFalse(loopExit);
		Statement(frame);
		CodeGen.Branch(loopStart);
		loopExit.Here();
	}

	static void HaltStatement() {
		Expect(halt_Sym);
		CodeGen.LeaveProgram();
		ExpectWeak(semicolon_Sym, 6);
	}

	static void ReturnStatement() {
		Expect(return_Sym);
		CodeGen.LeaveVoidFunction();
		ExpectWeak(semicolon_Sym, 6);
	}

	static void ReadStatement() {
		Expect(read_Sym);
		Expect(lparen_Sym);
		ReadList();
		Expect(rparen_Sym);
		ExpectWeak(semicolon_Sym, 6);
	}

	static void WriteStatement() {
		Expect(write_Sym);
		Expect(lparen_Sym);
		WriteList();
		Expect(rparen_Sym);
		ExpectWeak(semicolon_Sym, 6);
	}

	static void SwitchStatement(StackFrame frame) {
		int expType;
		Expect(switch_Sym);
		Expect(lparen_Sym);
		Expression(out expType);
		Expect(rparen_Sym);
		Expect(lbrace_Sym);
		while (la.kind == case_Sym) {
			CaseLabelList();
			Statement(frame);
			while (StartOf(3)) {
				Statement(frame);
			}
		}
		if (la.kind == default_Sym) {
			Get();
			Expect(colon_Sym);
			while (StartOf(3)) {
				Statement(frame);
			}
		}
		Expect(rbrace_Sym);
	}

	static void OneConst() {
		Entry constant = new Entry();
		ConstRec con;
		Ident(out constant.name);
		constant.kind = Kinds.Con;
		AssignOp();
		Constant(out con);
		constant.value = con.value;
		constant.type = con.type;
		Table.Insert(constant);
	}

	static void AssignOp() {
		Expect(equal_Sym);
	}

	static void Constant(out ConstRec con) {
		con = new ConstRec();
		if (la.kind == number_Sym) {
			IntConst(out con.value);
			con.type = Types.intType;
		} else if (la.kind == charLit_Sym) {
			CharConst(out con.value);
			con.type = Types.charType;
		} else if (la.kind == true_Sym) {
			Get();
			con.type = Types.boolType; con.value = 1;
		} else if (la.kind == false_Sym) {
			Get();
			con.type = Types.boolType; con.value = 0;
		} else if (la.kind == null_Sym) {
			Get();
			con.type = Types.nullType; con.value = 0;
		} else SynErr(52);
	}

	static void IntConst(out int value) {
		Expect(number_Sym);
		try {
		  value = Convert.ToInt32(token.val);
		} catch (Exception) {
		  value = 0; SemError("number out of range");
		}
	}

	static void CharConst(out int value) {
		Expect(charLit_Sym);
		string str = token.val;
		str = Unescape(str.Substring(1, str.Length - 2));
		value = str[0];
	}

	static void VarList(StackFrame frame, int type) {
		OneVar(frame, type);
		while (WeakSeparator(comma_Sym, 7, 8)) {
			OneVar(frame, type);
		}
	}

	static void BasicType(out int type) {
		type = Types.noType;
		if (la.kind == int_Sym) {
			Get();
			type = Types.intType;
		} else if (la.kind == bool_Sym) {
			Get();
			type = Types.boolType;
		} else if (la.kind == char_Sym) {
			Get();
			type = Types.charType;
		} else SynErr(53);
	}

	static void OneVar(StackFrame frame, int type) {
		int expType;
		Entry var = new Entry();
		Ident(out var.name);
		var.kind = Kinds.Var;
		var.type = type;
		var.offset = frame.size;
		frame.size++;
		if (la.kind == equal_Sym) {
			AssignOp();
			CodeGen.LoadAddress(var);
			Expression(out expType);
			if (!Assignable(var.type, expType))
			  SemError("incompatible types in assignment");
			CodeGen.Assign(var.type);
		}
		Table.Insert(var);
	}

	static void Expression(out int type) {
		int type2;
		Label shortcircuit = new Label(!known);
		AndExp(out type);
		while (la.kind == barbar_Sym) {
			Get();
			CodeGen.BooleanOp(shortcircuit, CodeGen.or);
			AndExp(out type2);
			if (!IsBool(type) || !IsBool(type2))
			  SemError("Boolean operands needed");
			type = Types.boolType;
		}
		shortcircuit.Here();
	}

	static void Arguments(DesType des) {
		int argCount = 0;
		Entry fp = des.entry.firstParam;
		if (StartOf(9)) {
			OneArg(fp);
			argCount++; if (fp != null) fp = fp.nextInScope;
			while (WeakSeparator(comma_Sym, 9, 2)) {
				OneArg(fp);
				argCount++; if (fp != null) fp = fp.nextInScope;
			}
		}
		if (argCount != des.entry.nParams)
		  SemError("wrong number of arguments");
	}

	static void Designator(out DesType des) {
		string name;
		int indexType;
		Ident(out name);
		Entry entry = Table.Find(name);
		if (!entry.declared)
		  SemError("undeclared identifier");
		des = new DesType(entry);
		if (entry.kind == Kinds.Var)
		  CodeGen.LoadAddress(entry);
		if (la.kind == lbrack_Sym) {
			Get();
			if (IsArray(des.type)) des.type--;
			else SemError("unexpected subscript");
			if (des.entry.kind != Kinds.Var)
			  SemError("unexpected subscript");
			CodeGen.Dereference();
			Expression(out indexType);
			if (!IsArith(indexType))
			  SemError("invalid subscript type");
			CodeGen.Index();
			Expect(rbrack_Sym);
		}
	}

	static void OneArg(Entry fp) {
		int argType;
		Expression(out argType);
		if (fp != null && !Assignable(fp.type, argType))
		    SemError("argument type mismatch");
	}

	static void Condition() {
		int type;
		Expression(out type);
		if (!IsBool(type))
		  SemError("Boolean expression needed");
	}

	static void CaseLabelList() {
		CaseLabel();
		while (la.kind == case_Sym) {
			CaseLabel();
		}
	}

	static void CaseLabel() {
		ConstRec con;
		Expect(case_Sym);
		if (la.kind == plus_Sym || la.kind == minus_Sym) {
			if (la.kind == plus_Sym) {
				Get();
			} else {
				Get();
			}
		}
		Constant(out con);
		Expect(colon_Sym);
	}

	static void ReadList() {
		ReadElement();
		while (WeakSeparator(comma_Sym, 10, 2)) {
			ReadElement();
		}
	}

	static void ReadElement() {
		string str;
		DesType des;
		if (la.kind == stringLit_Sym) {
			StringConst(out str);
			CodeGen.WriteString(str);
		} else if (la.kind == identifier_Sym) {
			Designator(out des);
			if (des.entry.kind != Kinds.Var)
			  SemError("wrong kind of identifier");
			switch (des.type) {
			  case Types.intType:
			  case Types.boolType:
			  case Types.charType:
			    CodeGen.Read(des.type); break;
			  default:
			    SemError("cannot read this type"); break;
			}
		} else SynErr(54);
	}

	static void StringConst(out string str) {
		Expect(stringLit_Sym);
		str = token.val;
		str = Unescape(str.Substring(1, str.Length - 2));
	}

	static void WriteList() {
		WriteElement();
		while (WeakSeparator(comma_Sym, 11, 2)) {
			WriteElement();
		}
	}

	static void WriteElement() {
		int expType;
		string str;
		if (la.kind == stringLit_Sym) {
			StringConst(out str);
			CodeGen.WriteString(str);
		} else if (StartOf(9)) {
			Expression(out expType);
			if (!(IsArith(expType) || expType == Types.boolType))
			  SemError("cannot write this type");
			switch (expType) {
			  case Types.intType:
			  case Types.boolType:
			  case Types.charType:
			    CodeGen.Write(expType); break;
			  default:
			    break;
			}
		} else SynErr(55);
	}

	static void AndExp(out int type) {
		int type2;
		Label shortcircuit = new Label(!known);
		EqlExp(out type);
		while (la.kind == andand_Sym) {
			Get();
			CodeGen.BooleanOp(shortcircuit, CodeGen.and);
			EqlExp(out type2);
			if (!IsBool(type) || !IsBool(type2))
			  SemError("Boolean operands needed");
			type = Types.boolType;
		}
		shortcircuit.Here();
	}

	static void EqlExp(out int type) {
		int type2;
		int op;
		RelExp(out type);
		while (la.kind == equalequal_Sym || la.kind == bangequal_Sym) {
			EqualOp(out op);
			RelExp(out type2);
			if (!Compatible(type, type2))
			  SemError("incomparable operand types");
			CodeGen.Comparison(op, type);
			type = Types.boolType;
		}
	}

	static void RelExp(out int type) {
		int type2;
		int op;
		AddExp(out type);
		if (StartOf(12)) {
			RelOp(out op);
			AddExp(out type2);
			if (!IsArith(type) || !IsArith(type2))
			  SemError("incomparable operand types");
			CodeGen.Comparison(op, type);
			type = Types.boolType;
		}
	}

	static void EqualOp(out int op) {
		op = CodeGen.nop;
		if (la.kind == equalequal_Sym) {
			Get();
			op = CodeGen.ceq;
		} else if (la.kind == bangequal_Sym) {
			Get();
			op = CodeGen.cne;
		} else SynErr(56);
	}

	static void AddExp(out int type) {
		int type2;
		int op;
		MultExp(out type);
		while (la.kind == plus_Sym || la.kind == minus_Sym) {
			AddOp(out op);
			MultExp(out type2);
			if (IsArith(type) && IsArith(type2)) {
			  type = Types.intType;
			  CodeGen.BinaryOp(op);
			}
			else {
			  SemError("arithmetic operands needed");
			  type = Types.noType;
			}
		}
	}

	static void RelOp(out int op) {
		op = CodeGen.nop;
		if (la.kind == less_Sym) {
			Get();
			op = CodeGen.clt;
		} else if (la.kind == lessequal_Sym) {
			Get();
			op = CodeGen.cle;
		} else if (la.kind == greater_Sym) {
			Get();
			op = CodeGen.cgt;
		} else if (la.kind == greaterequal_Sym) {
			Get();
			op = CodeGen.cge;
		} else SynErr(57);
	}

	static void MultExp(out int type) {
		int type2;
		int op;
		Factor(out type);
		while (la.kind == star_Sym || la.kind == slash_Sym || la.kind == percent_Sym) {
			MulOp(out op);
			Factor(out type2);
			if (IsArith(type) && IsArith(type2)) {
			  type = Types.intType;
			  CodeGen.BinaryOp(op);
			}
			else {
			  SemError("arithmetic operands needed");
			  type = Types.noType;
			}
		}
	}

	static void AddOp(out int op) {
		op = CodeGen.nop;
		if (la.kind == plus_Sym) {
			Get();
			op = CodeGen.add;
		} else if (la.kind == minus_Sym) {
			Get();
			op = CodeGen.sub;
		} else SynErr(58);
	}

	static void Factor(out int type) {
		type = Types.noType;
		if (StartOf(13)) {
			Primary(out type);
		} else if (la.kind == plus_Sym) {
			Get();
			Factor(out type);
			if (!IsArith(type)) {
			  SemError("arithmetic operand needed");
			  type = Types.noType;
			}
			else type = Types.intType;
		} else if (la.kind == minus_Sym) {
			Get();
			Factor(out type);
			if (!IsArith(type)) {
			  SemError("arithmetic operand needed");
			  type = Types.noType;
			}
			else type = Types.intType;
			CodeGen.NegateInteger();
		} else if (la.kind == bang_Sym) {
			Get();
			Factor(out type);
			if (!IsBool(type))
			  SemError("Boolean operand needed");
			type = Types.boolType;
			CodeGen.NegateBoolean();
		} else SynErr(59);
	}

	static void MulOp(out int op) {
		op = CodeGen.nop;
		if (la.kind == star_Sym) {
			Get();
			op = CodeGen.mul;
		} else if (la.kind == slash_Sym) {
			Get();
			op = CodeGen.div;
		} else if (la.kind == percent_Sym) {
			Get();
			op = CodeGen.rem;
		} else SynErr(60);
	}

	static void Primary(out int type) {
		type = Types.noType;
		int size;
		DesType des;
		ConstRec con;
		if (la.kind == identifier_Sym) {
			Designator(out des);
			type = des.type;
			switch (des.entry.kind) {
			  case Kinds.Var:
			    CodeGen.Dereference();
			    break;
			  case Kinds.Con:
			    CodeGen.LoadConstant(des.entry.value);
			    break;
			  default:
			    SemError("wrong kind of identifier");
			    break;
			}
		} else if (StartOf(14)) {
			Constant(out con);
			type = con.type;
			CodeGen.LoadConstant(con.value);
		} else if (la.kind == new_Sym) {
			Get();
			BasicType(out type);
			Expect(lbrack_Sym);
			Expression(out size);
			if (!IsArith(size))
			  SemError("array size must be integer");
			type++;
			Expect(rbrack_Sym);
			CodeGen.Allocate();
		} else if (la.kind == lparen_Sym) {
			Get();
			if (la.kind == char_Sym) {
				Get();
				Expect(rparen_Sym);
				Factor(out type);
				if (!IsArith(type))
				  SemError("invalid cast");
				else type = Types.charType;
				CodeGen.CastToChar();
			} else if (la.kind == int_Sym) {
				Get();
				Expect(rparen_Sym);
				Factor(out type);
				if (!IsArith(type))
				  SemError("invalid cast");
				else type = Types.intType;
			} else if (StartOf(9)) {
				Expression(out type);
				Expect(rparen_Sym);
			} else SynErr(61);
		} else SynErr(62);
	}



	public static void Parse() {
		la = new Token();
		la.val = "";
		Get();
		Parva();
		Expect(EOF_SYM);

	}

	static bool[,] set = {
		{T,T,x,x, x,x,x,x, x,T,x,T, T,x,x,x, x,T,T,T, x,x,T,T, T,x,x,x, x,x,T,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,T,x,x, x,x,x,x, x,T,x,T, T,x,x,x, x,T,T,T, x,x,T,T, T,x,x,x, x,x,T,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{T,T,x,x, x,T,x,x, x,T,x,T, T,x,x,x, x,T,T,T, x,x,T,T, T,x,x,x, x,x,T,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{T,T,x,x, x,x,x,x, x,T,x,T, T,x,x,x, x,T,T,T, x,x,T,T, T,x,x,x, x,x,T,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{T,T,x,x, x,x,x,x, x,T,T,T, T,x,x,x, x,T,T,T, x,x,T,T, T,T,x,T, x,x,T,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,T,T,x, T,x,T,x, x,x,x,x, x,T,T,T, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,x,x, T,T,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,T,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,T,T,T, T,x,T,x, x,x,x,x, x,T,T,T, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,x,x, T,T,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,x, x,x},
		{x,T,T,x, T,x,T,x, x,x,x,x, x,T,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,x,T,x, T,x,x,x, x,x,x,x, x,T,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x}

	};

} // end Parser

/* pdt - considerable extension from here on */

public class ErrorRec {
	public int line, col, num;
	public string str;
	public ErrorRec next;

	public ErrorRec(int l, int c, string s) {
		line = l; col = c; str = s; next = null;
	}

} // end ErrorRec

public class Errors {

	public static int count = 0;                                     // number of errors detected
	public static int warns = 0;                                     // number of warnings detected
	public static string errMsgFormat = "file {0} : ({1}, {2}) {3}"; // 0=file 1=line, 2=column, 3=text
	static string fileName = "";
	static string listName = "";
	static bool mergeErrors = false;
	static StreamWriter mergedList;

	static ErrorRec first = null, last;
	static bool eof = false;

	static string GetLine() {
		char ch, CR = '\r', LF = '\n';
		int l = 0;
		StringBuilder s = new StringBuilder();
		ch = (char) Buffer.Read();
		while (ch != Buffer.EOF && ch != CR && ch != LF) {
			s.Append(ch); l++; ch = (char) Buffer.Read();
		}
		eof = (l == 0 && ch == Buffer.EOF);
		if (ch == CR) {  // check for MS-DOS
			ch = (char) Buffer.Read();
			if (ch != LF && ch != Buffer.EOF) Buffer.Pos--;
		}
		return s.ToString();
	}

	static void Display (string s, ErrorRec e) {
		mergedList.Write("**** ");
		for (int c = 1; c < e.col; c++)
			if (s[c-1] == '\t') mergedList.Write("\t"); else mergedList.Write(" ");
		mergedList.WriteLine("^ " + e.str);
	}

	public static void Init (string fn, string dir, bool merge) {
		fileName = fn;
		listName = dir + "listing.txt";
		mergeErrors = merge;
		if (mergeErrors)
			try {
				mergedList = new StreamWriter(new FileStream(listName, FileMode.Create));
			} catch (IOException) {
				Errors.Exception("-- could not open " + listName);
			}
	}

	public static void Summarize () {
		if (mergeErrors) {
			mergedList.WriteLine();
			ErrorRec cur = first;
			Buffer.Pos = 0;
			int lnr = 1;
			string s = GetLine();
			while (!eof) {
				mergedList.WriteLine("{0,4} {1}", lnr, s);
				while (cur != null && cur.line == lnr) {
					Display(s, cur); cur = cur.next;
				}
				lnr++; s = GetLine();
			}
			if (cur != null) {
				mergedList.WriteLine("{0,4}", lnr);
				while (cur != null) {
					Display(s, cur); cur = cur.next;
				}
			}
			mergedList.WriteLine();
			mergedList.WriteLine(count + " errors detected");
			if (warns > 0) mergedList.WriteLine(warns + " warnings detected");
			mergedList.Close();
		}
		switch (count) {
			case 0 : Console.WriteLine("Parsed correctly"); break;
			case 1 : Console.WriteLine("1 error detected"); break;
			default: Console.WriteLine(count + " errors detected"); break;
		}
		if (warns > 0) Console.WriteLine(warns + " warnings detected");
		if ((count > 0 || warns > 0) && mergeErrors) Console.WriteLine("see " + listName);
	}

	public static void StoreError (int line, int col, string s) {
		if (mergeErrors) {
			ErrorRec latest = new ErrorRec(line, col, s);
			if (first == null) first = latest; else last.next = latest;
			last = latest;
		} else Console.WriteLine(errMsgFormat, fileName, line, col, s);
	}

	public static void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "identifier expected"; break;
			case 2: s = "number expected"; break;
			case 3: s = "stringLit expected"; break;
			case 4: s = "charLit expected"; break;
			case 5: s = "\"void\" expected"; break;
			case 6: s = "\"(\" expected"; break;
			case 7: s = "\")\" expected"; break;
			case 8: s = "\",\" expected"; break;
			case 9: s = "\"{\" expected"; break;
			case 10: s = "\"}\" expected"; break;
			case 11: s = "\";\" expected"; break;
			case 12: s = "\"const\" expected"; break;
			case 13: s = "\"true\" expected"; break;
			case 14: s = "\"false\" expected"; break;
			case 15: s = "\"null\" expected"; break;
			case 16: s = "\"[]\" expected"; break;
			case 17: s = "\"int\" expected"; break;
			case 18: s = "\"bool\" expected"; break;
			case 19: s = "\"char\" expected"; break;
			case 20: s = "\"[\" expected"; break;
			case 21: s = "\"]\" expected"; break;
			case 22: s = "\"if\" expected"; break;
			case 23: s = "\"while\" expected"; break;
			case 24: s = "\"switch\" expected"; break;
			case 25: s = "\"default\" expected"; break;
			case 26: s = "\":\" expected"; break;
			case 27: s = "\"case\" expected"; break;
			case 28: s = "\"+\" expected"; break;
			case 29: s = "\"-\" expected"; break;
			case 30: s = "\"halt\" expected"; break;
			case 31: s = "\"return\" expected"; break;
			case 32: s = "\"read\" expected"; break;
			case 33: s = "\"write\" expected"; break;
			case 34: s = "\"||\" expected"; break;
			case 35: s = "\"&&\" expected"; break;
			case 36: s = "\"!\" expected"; break;
			case 37: s = "\"new\" expected"; break;
			case 38: s = "\"*\" expected"; break;
			case 39: s = "\"/\" expected"; break;
			case 40: s = "\"%\" expected"; break;
			case 41: s = "\"==\" expected"; break;
			case 42: s = "\"!=\" expected"; break;
			case 43: s = "\"<\" expected"; break;
			case 44: s = "\"<=\" expected"; break;
			case 45: s = "\">\" expected"; break;
			case 46: s = "\">=\" expected"; break;
			case 47: s = "\"=\" expected"; break;
			case 48: s = "??? expected"; break;
			case 49: s = "this symbol not expected in Statement"; break;
			case 50: s = "invalid Statement"; break;
			case 51: s = "invalid AssignmentOrCall"; break;
			case 52: s = "invalid Constant"; break;
			case 53: s = "invalid BasicType"; break;
			case 54: s = "invalid ReadElement"; break;
			case 55: s = "invalid WriteElement"; break;
			case 56: s = "invalid EqualOp"; break;
			case 57: s = "invalid RelOp"; break;
			case 58: s = "invalid AddOp"; break;
			case 59: s = "invalid Factor"; break;
			case 60: s = "invalid MulOp"; break;
			case 61: s = "invalid Primary"; break;
			case 62: s = "invalid Primary"; break;

			default: s = "error " + n; break;
		}
		StoreError(line, col, s);
		count++;
	}

	public static void SemErr (int line, int col, int n) {
		StoreError(line, col, ("error " + n));
		count++;
	}

	public static void Error (int line, int col, string s) {
		StoreError(line, col, s);
		count++;
	}

	public static void Error (string s) {
		if (mergeErrors) mergedList.WriteLine(s); else Console.WriteLine(s);
		count++;
	}

	public static void Warn (int line, int col, string s) {
		StoreError(line, col, s);
		warns++;
	}

	public static void Warn (string s) {
		if (mergeErrors) mergedList.WriteLine(s); else Console.WriteLine(s);
		warns++;
	}

	public static void Exception (string s) {
		Console.WriteLine(s);
		System.Environment.Exit(1);
	}

} // end Errors

} // end namespace
