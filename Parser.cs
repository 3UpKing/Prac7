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
	public const int maxT = 59;
	public const int _CodeOn = 60;
	public const int _CodeOff = 61;
	public const int _DebugOn = 62;
	public const int _DebugOff = 63;
	public const int _StackDump = 64;
	public const int _HeapDump = 65;
	public const int _TableDump = 66;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;

	public static Token token;    // last recognized token   /* pdt */
	public static Token la;       // lookahead token
	static int errDist = minErrDist;

	public static bool  // set/used by pragmas or cmd line args
    debug    = false,
    optimize = false,
    listCode = false,
    warnings = true;

  const bool
    known = true;

  // This next method might better be located in the code
  // generator.  Traditionally it has been left in the ATG
  // file, but that might change in future years.
  //
  // Note that while sequences like \n \r and \t result in
  // special mappings to lf, cr and tab, other sequences
  // like \x \: and \9 simply map to x, ; and 9 .
  // Most students don't seem to know this!

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

/* -------------------------------------------------------- */



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
				if (la.kind == 60) {
				listCode  = true;
				}
				if (la.kind == 61) {
				listCode  = false;
				}
				if (la.kind == 62) {
				debug     = true;
				}
				if (la.kind == 63) {
				debug     = false;
				}
				if (la.kind == 64) {
				if(debug) CodeGen.Stack();
				}
				if (la.kind == 65) {
				if(debug) CodeGen.Heap();
				}
				if (la.kind == 66) {
				if(debug) Table.PrintTable(OutFile.StdOut);
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
		CodeGen.FrameHeader();
		// no arguments
		CodeGen.Call(mainEntryPoint);
		// forward, incomplete
		CodeGen.LeaveProgram();
		while (la.kind == 5) {
			FuncDeclaration();
		}
		Expect(0);
		if
		(!mainEntryPoint.IsDefined())
		SemError("missing Main function");
	}

	static void FuncDeclaration() {
		StackFrame frame = new StackFrame();
		Entry function = new Entry();
		Expect(5);
		Ident(out function.name);
		function.kind = Kinds.Fun;
		                      function.type = Types.voidType;
		                      function.nParams = 0;
		                      function.firstParam = null;
		           function.entryPoint = new Label(known);
		                      Table.Insert(function);
		                      Table.OpenScope();
		Expect(6);
		FormalParameters(function);
		Expect(7);
		frame.size = CodeGen.headerSize +
		function.nParams;
		            if (function.name.ToUpper().Equals("MAIN")
		                && !mainEntryPoint.IsDefined()
		                && function.nParams == 0) {
		                    mainEntryPoint.Here(); }
		Body(frame);
		Table.CloseScope();
	}

	static void Ident(out string name) {
		Expect(1);
		name = token.val;
	}

	static void FormalParameters(Entry func) {
		Entry param;
		if (la.kind == 18 || la.kind == 19 || la.kind == 20) {
			OneParam(out param, func);
			func.firstParam = param;
			while (WeakSeparator(8, 1, 2)) {
				OneParam(out param, func);
			}
		}
	}

	static void Body(StackFrame frame) {
		Label DSPLabel = new Label(known);
		int sizeMark = frame.size;
		CodeGen.OpenStackFrame(0);
		Expect(9);
		while (StartOf(3)) {
			Statement(frame);
		}
		ExpectWeak(10, 4);
		CodeGen.FixDSP(DSPLabel.Address(),
		frame.size - sizeMark);
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
		if (la.kind == 17) {
			Get();
			type++;
		}
	}

	static void Statement(StackFrame frame) {
		string msg = "";
		while (!(StartOf(5))) {SynErr(60); Get();}
		switch (la.kind) {
		case 9: {
			Block(frame);
			break;
		}
		case 13: {
			ConstDeclarations();
			break;
		}
		case 18: case 19: case 20: {
			VarDeclarations(frame);
			break;
		}
		case 1: {
			AssignmentOrCall();
			break;
		}
		case 23: {
			IfStatement(frame);
			break;
		}
		case 27: {
			WhileStatement(frame);
			break;
		}
		case 28: {
			DoWhileStatement(frame);
			break;
		}
		case 29: {
			RepeatStatement(frame);
			break;
		}
		case 37: {
			HaltStatement(msg);
			break;
		}
		case 38: {
			ReturnStatement();
			break;
		}
		case 39: case 40: {
			ReadStatement();
			break;
		}
		case 41: case 42: {
			WriteStatement();
			break;
		}
		case 31: {
			SwitchStatement(frame);
			break;
		}
		case 12: {
			BreakStatement(frame);
			break;
		}
		case 11: {
			Get();
			break;
		}
		default: SynErr(61); break;
		}
	}

	static void Block(StackFrame frame) {
		Table.OpenScope();
		Expect(9);
		while (StartOf(3)) {
			Statement(frame);
		}
		ExpectWeak(10, 6);
		Table.CloseScope();
	}

	static void ConstDeclarations() {
		Expect(13);
		OneConst();
		while (WeakSeparator(8, 7, 8)) {
			OneConst();
		}
		ExpectWeak(11, 6);
	}

	static void VarDeclarations(StackFrame frame) {
		int type;
		Type(out type);
		VarList(frame, type);
		ExpectWeak(11, 6);
	}

	static void AssignmentOrCall() {
		int expType;
		DesType des;
		if (IsCall(out des)) {
			Expect(1);
			CodeGen.FrameHeader();
			Expect(6);
			Arguments(des);
			Expect(7);
			CodeGen.Call
			(des.entry.entryPoint);
		} else if (la.kind == 1) {
			Designator(out des);
			if (des.entry.kind !=
			Kinds.Var)
			SemError("cannot assign to " +
			Kinds.kindNames[des.entry.kind]);
			AssignOp();
			Expression(out expType);
			if (!Assignable(des.type,
			expType))
			SemError("incompatible types in assignment");
			CodeGen.Assign(des.type);
		} else SynErr(62);
		ExpectWeak(11, 6);
	}

	static void IfStatement(StackFrame frame) {
		Label falseLabel =
		new Label(!known);
		Label ifExit = new Label(!known);
		Expect(23);
		Expect(6);
		Condition();
		Expect(7);
		if (la.kind == 24) {
			Get();
			SemError("Should 'then' be here?");
		}
		CodeGen.BranchFalse
		(falseLabel);
		Statement(frame);
		CodeGen.Branch(ifExit) ; falseLabel.Here();
		while (la.kind == 25) {
			Label elsIfExit = new Label (!known);
			Get();
			Expect(6);
			Condition();
			Expect(7);
			CodeGen.BranchFalse(elsIfExit);
			Statement(frame);
			CodeGen.Branch(ifExit) ; elsIfExit.Here();
		}
		if (la.kind == 26) {
			Get();
			Statement(frame);
		}
		ifExit.Here();
	}

	static void WhileStatement(StackFrame frame) {
		Label loopExit  =
		new Label(!known);
		Label loopStart =
		new Label(known);
		Expect(27);
		Expect(6);
		Condition();
		Expect(7);
		CodeGen.BranchFalse
		(loopExit);
		Statement(frame);
		CodeGen.Branch
		(loopStart);
		loopExit.Here();
	}

	static void DoWhileStatement(StackFrame frame) {
		Label loopExit  =
		new Label(!known);
		Label loopStart =
		new Label(known);
		Expect(28);
		Statement(frame);
		Expect(27);
		Expect(6);
		Condition();
		Expect(7);
		CodeGen.BranchFalse(loopExit);
		CodeGen.Branch(loopStart);
		loopExit.Here();
	}

	static void RepeatStatement(StackFrame frame) {
		Label loopExit  =
		new Label(!known);
		Label loopStart =
		new Label(known);
		Expect(29);
		while (StartOf(3)) {
			Statement(frame);
		}
		Expect(30);
		Expect(6);
		Condition();
		Expect(7);
		CodeGen.BranchFalse(loopStart);
		CodeGen.Branch(loopExit);
		loopExit.Here();
		
	}

	static void HaltStatement(string printMsg = "") {
		Expect(37);
		CodeGen.WriteString(printMsg); CodeGen.LeaveProgram();
		ExpectWeak(11, 6);
	}

	static void ReturnStatement() {
		Expect(38);
		CodeGen.LeaveVoidFunction();
		ExpectWeak(11, 6);
	}

	static void ReadStatement() {
		if (la.kind == 39) {
			Get();
			Expect(6);
			ReadList();
			Expect(7);
			ExpectWeak(11, 6);
		} else if (la.kind == 40) {
			Get();
			Expect(6);
			ReadListLine();
			Expect(7);
			ExpectWeak(11, 6);
		} else SynErr(63);
	}

	static void WriteStatement() {
		if (la.kind == 41) {
			Get();
			Expect(6);
			WriteList();
			Expect(7);
			ExpectWeak(11, 6);
		} else if (la.kind == 42) {
			Get();
			Expect(6);
			WriteListLine();
			Expect(7);
			ExpectWeak(11, 6);
		} else SynErr(64);
	}

	static void SwitchStatement(StackFrame frame) {
		int expType;
		Expect(31);
		Expect(6);
		Expression(out expType);
		Expect(7);
		Expect(9);
		while (la.kind == 34) {
			CaseLabelList();
			Statement(frame);
			while (StartOf(3)) {
				Statement(frame);
			}
		}
		if (la.kind == 32) {
			Get();
			Expect(33);
			while (StartOf(3)) {
				Statement(frame);
			}
		}
		Expect(10);
	}

	static void BreakStatement(StackFrame frame) {
		Expect(12);
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
		if (la.kind == 52) {
			Get();
		} else if (la.kind == 58) {
			Get();
			SemError("Did you mean '=' ?");
		} else SynErr(65);
	}

	static void Constant(out ConstRec con) {
		con = new ConstRec();
		if (la.kind == 2) {
			IntConst(out con.value);
			con.type = Types.intType;
		} else if (la.kind == 4) {
			CharConst(out con.value);
			con.type = Types.charType;
		} else if (la.kind == 14) {
			Get();
			con.type = Types.boolType;
			con.value = 1;
		} else if (la.kind == 15) {
			Get();
			con.type = Types.boolType;
			con.value = 0;
		} else if (la.kind == 16) {
			Get();
			con.type = Types.nullType;
			con.value = 0;
		} else SynErr(66);
	}

	static void IntConst(out int value) {
		Expect(2);
		try {value =
		Convert.ToInt32(token.val);
		} catch (Exception) {
		  value = 0;
		SemError("number out of range");
		      }
	}

	static void CharConst(out int value) {
		Expect(4);
		string str = token.val;
		str = Unescape
		(str.Substring(1, str.Length - 2));
		value = str[0];
	}

	static void VarList(StackFrame frame, int type) {
		OneVar(frame, type);
		while (WeakSeparator(8, 7, 8)) {
			OneVar(frame, type);
		}
	}

	static void BasicType(out int type) {
		type = Types.noType;
		if (la.kind == 18) {
			Get();
			type = Types.intType;
		} else if (la.kind == 19) {
			Get();
			type = Types.boolType;
		} else if (la.kind == 20) {
			Get();
			type = Types.charType;
		} else SynErr(67);
	}

	static void OneVar(StackFrame frame, int type) {
		int expType;
		Entry var = new Entry();
		Ident(out var.name);
		var.kind = Kinds.Var;
		var.type = type;
		var.offset = frame.size;
		frame.size++;
		if (la.kind == 52 || la.kind == 58) {
			AssignOp();
			CodeGen.LoadAddress(var);
			Expression(out expType);
			if (!Assignable(var.type,
			expType))
			SemError("incompatible types in assignment");
			CodeGen.Assign(var.type);
		}
		Table.Insert(var);
	}

	static void Expression(out int type) {
		int type2;
		Label shortcircuit = new Label(!known);
		AndExp(out type);
		while (la.kind == 43) {
			Get();
			CodeGen.BooleanOp
			(shortcircuit, CodeGen.or);
			AndExp(out type2);
			if (!IsBool(type) ||
			!IsBool(type2))
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
			argCount++; if (fp != null)
			fp = fp.nextInScope;
			while (WeakSeparator(8, 9, 2)) {
				OneArg(fp);
				argCount++; if (fp != null)
				fp = fp.nextInScope;
			}
		}
		if (argCount !=
		des.entry.nParams)
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
		if (la.kind == 21) {
			Get();
			if (IsArray(des.type))
			des.type--;
			else
			SemError("unexpected subscript");
			 if (des.entry.kind !=
			Kinds.Var)
			SemError("unexpected subscript");
			 CodeGen.Dereference();
			Expression(out indexType);
			if (!IsArith(indexType))
			SemError("invalid subscript type");
			CodeGen.Index();
			Expect(22);
		}
	}

	static void OneArg(Entry fp) {
		int argType;
		Expression(out argType);
		if (fp != null &&
		!Assignable(fp.type, argType))
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
		while (la.kind == 34) {
			CaseLabel();
		}
	}

	static void CaseLabel() {
		ConstRec con;
		Expect(34);
		if (la.kind == 35 || la.kind == 36) {
			if (la.kind == 35) {
				Get();
			} else {
				Get();
			}
		}
		Constant(out con);
		Expect(33);
	}

	static void ReadList() {
		ReadElement();
		while (WeakSeparator(8, 10, 2)) {
			ReadElement();
		}
	}

	static void ReadListLine() {
		ReadElement();
		while (WeakSeparator(8, 10, 2)) {
			ReadElement();
		}
		CodeGen.ReadLine();
	}

	static void ReadElement() {
		string str;
		DesType des;
		if (la.kind == 3) {
			StringConst(out str);
			CodeGen.WriteString(str);
		} else if (la.kind == 1) {
			Designator(out des);
			if (des.entry.kind !=
			Kinds.Var)
			SemError("wrong kind of identifier");
			switch (des.type) {
			case Types.intType:
			case Types.boolType:
			case Types.charType:
			CodeGen.Read(des.type);
			break;
			default:
			SemError("cannot read this type");
			break;
			            }
		} else SynErr(68);
	}

	static void StringConst(out string str) {
		Expect(3);
		str = token.val;
		str = Unescape
		(str.Substring(1, str.Length - 2));
	}

	static void WriteList() {
		WriteElement();
		while (WeakSeparator(8, 11, 2)) {
			WriteElement();
		}
	}

	static void WriteListLine() {
		WriteElement();
		while (WeakSeparator(8, 11, 2)) {
			WriteElement();
		}
		CodeGen.WriteLine();
	}

	static void WriteElement() {
		int expType; string str;
		if (la.kind == 3) {
			StringConst(out str);
			CodeGen.WriteString(str);
		} else if (StartOf(9)) {
			Expression(out expType);
			if (!(IsArith(expType) ||
			expType == Types.boolType))
			SemError("cannot write this type");
			switch (expType) {
			case Types.intType:
			case Types.boolType:
			case Types.charType:
			  CodeGen.Write(expType);
			break;
			default:  break;
			                 }
		} else SynErr(69);
	}

	static void AndExp(out int type) {
		int type2;
		Label shortcircuit = new
		Label(!known);
		EqlExp(out type);
		while (la.kind == 44) {
			Get();
			CodeGen.BooleanOp
			(shortcircuit, CodeGen.and);
			EqlExp(out type2);
			if (!IsBool(type) ||
			!IsBool(type2))
			SemError("Boolean operands needed");
			type = Types.boolType;
		}
		shortcircuit.Here();
	}

	static void EqlExp(out int type) {
		int type2;
		int op;
		RelExp(out type);
		while (StartOf(12)) {
			EqualOp(out op);
			RelExp(out type2);
			if (!Compatible(type, type2))
			SemError("incomparable operand types");
			CodeGen.Comparison(op, type);
			type = Types.boolType;
		}
	}

	static void RelExp(out int type) {
		int type2; int op;
		AddExp(out type);
		if (StartOf(13)) {
			RelOp(out op);
			AddExp(out type2);
			if (!IsArith(type) ||
			!IsArith(type2))
			SemError("incomparable operand types");
			CodeGen.Comparison(op, type);
			type = Types.boolType;
		}
	}

	static void EqualOp(out int op) {
		op = CodeGen.nop;
		if (la.kind == 50) {
			Get();
			op = CodeGen.ceq;
		} else if (la.kind == 51) {
			Get();
			op = CodeGen.cne;
		} else if (la.kind == 52) {
			Get();
			SemError("Did you mean == ?"); op = CodeGen.ceq;
		} else if (la.kind == 53) {
			Get();
			SemError("Did you mean != ?"); op = CodeGen.cne;
		} else SynErr(70);
	}

	static void AddExp(out int type) {
		int type2; int op;
		MultExp(out type);
		while (la.kind == 35 || la.kind == 36) {
			AddOp(out op);
			MultExp(out type2);
			if (IsArith(type) &&
			IsArith(type2)) {
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
		if (la.kind == 54) {
			Get();
			op = CodeGen.clt;
		} else if (la.kind == 55) {
			Get();
			op = CodeGen.cle;
		} else if (la.kind == 56) {
			Get();
			op = CodeGen.cgt;
		} else if (la.kind == 57) {
			Get();
			op = CodeGen.cge;
		} else SynErr(71);
	}

	static void MultExp(out int type) {
		int type2; int op;
		Factor(out type);
		while (la.kind == 47 || la.kind == 48 || la.kind == 49) {
			MulOp(out op);
			Factor(out type2);
			if (IsArith(type) &&
			IsArith(type2)) {
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
		if (la.kind == 35) {
			Get();
			op = CodeGen.add;
		} else if (la.kind == 36) {
			Get();
			op = CodeGen.sub;
		} else SynErr(72);
	}

	static void Factor(out int type) {
		type = Types.noType;
		if (StartOf(14)) {
			Primary(out type);
		} else if (la.kind == 35) {
			Get();
			Factor(out type);
			if (!IsArith(type)) {
			SemError("arithmetic operand needed");
			   type = Types.noType;
			 }
			else
			type = Types.intType;
		} else if (la.kind == 36) {
			Get();
			Factor(out type);
			if (!IsArith(type)) {
			SemError("arithmetic operand needed");
			   type = Types.noType;
			 }
			else
			type = Types.intType;
			CodeGen.NegateInteger();
		} else if (la.kind == 45) {
			Get();
			Factor(out type);
			if (!IsBool(type))
			SemError("Boolean operand needed");
			type = Types.boolType;
			CodeGen.NegateBoolean();
		} else SynErr(73);
	}

	static void MulOp(out int op) {
		op = CodeGen.nop;
		if (la.kind == 47) {
			Get();
			op = CodeGen.mul;
		} else if (la.kind == 48) {
			Get();
			op = CodeGen.div;
		} else if (la.kind == 49) {
			Get();
			op = CodeGen.rem;
		} else SynErr(74);
	}

	static void Primary(out int type) {
		type = Types.noType;
		int size;
		DesType des;
		ConstRec con;
		if (la.kind == 1) {
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
		} else if (StartOf(15)) {
			Constant(out con);
			type = con.type;
			CodeGen.LoadConstant(con.value);
		} else if (la.kind == 46) {
			Get();
			BasicType(out type);
			Expect(21);
			Expression(out size);
			if (!IsArith(size))
			SemError("array size must be integer");
			type++;
			Expect(22);
			CodeGen.Allocate();
		} else if (la.kind == 6) {
			Get();
			if (la.kind == 20) {
				Get();
				Expect(7);
				Factor(out type);
				if (!IsArith(type))
				SemError("invalid cast");
				else
				type = Types.charType;
				CodeGen.CastToChar();
			} else if (la.kind == 18) {
				Get();
				Expect(7);
				Factor(out type);
				if (!IsArith(type))
				SemError("invalid cast");
				else
				type = Types.intType;
			} else if (StartOf(9)) {
				Expression(out type);
				Expect(7);
			} else SynErr(75);
		} else SynErr(76);
	}



	public static void Parse() {
		la = new Token();
		la.val = "";
		Get();
		Parva();
		Expect(0);

	}

	static bool[,] set = {
		{T,T,x,x, x,x,x,x, x,T,x,T, T,T,x,x, x,x,T,T, T,x,x,T, x,x,x,T, T,T,x,T, x,x,x,x, x,T,T,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,T,x,x, x,x,x,x, x,T,x,T, T,T,x,x, x,x,T,T, T,x,x,T, x,x,x,T, T,T,x,T, x,x,x,x, x,T,T,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{T,T,x,x, x,T,x,x, x,T,x,T, T,T,x,x, x,x,T,T, T,x,x,T, x,x,x,T, T,T,x,T, x,x,x,x, x,T,T,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{T,T,x,x, x,x,x,x, x,T,x,T, T,T,x,x, x,x,T,T, T,x,x,T, x,x,x,T, T,T,x,T, x,x,x,x, x,T,T,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{T,T,x,x, x,x,x,x, x,T,T,T, T,T,x,x, x,x,T,T, T,x,x,T, x,T,T,T, T,T,T,T, T,x,T,x, x,T,T,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,T,T,x, T,x,T,x, x,x,x,x, x,x,T,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,x,x,x, x,x,x,x, x,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,T,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,T,T,T, T,x,T,x, x,x,x,x, x,x,T,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,x,x,x, x,x,x,x, x,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, T,T,x,x, x,x,x,x, x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, T,T,x,x, x},
		{x,T,T,x, T,x,T,x, x,x,x,x, x,x,T,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x},
		{x,x,T,x, T,x,x,x, x,x,x,x, x,x,T,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x}

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
			case 12: s = "\"break\" expected"; break;
			case 13: s = "\"const\" expected"; break;
			case 14: s = "\"true\" expected"; break;
			case 15: s = "\"false\" expected"; break;
			case 16: s = "\"null\" expected"; break;
			case 17: s = "\"[]\" expected"; break;
			case 18: s = "\"int\" expected"; break;
			case 19: s = "\"bool\" expected"; break;
			case 20: s = "\"char\" expected"; break;
			case 21: s = "\"[\" expected"; break;
			case 22: s = "\"]\" expected"; break;
			case 23: s = "\"if\" expected"; break;
			case 24: s = "\"then\" expected"; break;
			case 25: s = "\"elsif\" expected"; break;
			case 26: s = "\"else\" expected"; break;
			case 27: s = "\"while\" expected"; break;
			case 28: s = "\"do\" expected"; break;
			case 29: s = "\"repeat\" expected"; break;
			case 30: s = "\"until\" expected"; break;
			case 31: s = "\"switch\" expected"; break;
			case 32: s = "\"default\" expected"; break;
			case 33: s = "\":\" expected"; break;
			case 34: s = "\"case\" expected"; break;
			case 35: s = "\"+\" expected"; break;
			case 36: s = "\"-\" expected"; break;
			case 37: s = "\"halt\" expected"; break;
			case 38: s = "\"return\" expected"; break;
			case 39: s = "\"read\" expected"; break;
			case 40: s = "\"readLine\" expected"; break;
			case 41: s = "\"write\" expected"; break;
			case 42: s = "\"writeLine\" expected"; break;
			case 43: s = "\"||\" expected"; break;
			case 44: s = "\"&&\" expected"; break;
			case 45: s = "\"!\" expected"; break;
			case 46: s = "\"new\" expected"; break;
			case 47: s = "\"*\" expected"; break;
			case 48: s = "\"/\" expected"; break;
			case 49: s = "\"%\" expected"; break;
			case 50: s = "\"==\" expected"; break;
			case 51: s = "\"!=\" expected"; break;
			case 52: s = "\"=\" expected"; break;
			case 53: s = "\"<>\" expected"; break;
			case 54: s = "\"<\" expected"; break;
			case 55: s = "\"<=\" expected"; break;
			case 56: s = "\">\" expected"; break;
			case 57: s = "\">=\" expected"; break;
			case 58: s = "\":=\" expected"; break;
			case 59: s = "??? expected"; break;
			case 60: s = "this symbol not expected in Statement"; break;
			case 61: s = "invalid Statement"; break;
			case 62: s = "invalid AssignmentOrCall"; break;
			case 63: s = "invalid ReadStatement"; break;
			case 64: s = "invalid WriteStatement"; break;
			case 65: s = "invalid AssignOp"; break;
			case 66: s = "invalid Constant"; break;
			case 67: s = "invalid BasicType"; break;
			case 68: s = "invalid ReadElement"; break;
			case 69: s = "invalid WriteElement"; break;
			case 70: s = "invalid EqualOp"; break;
			case 71: s = "invalid RelOp"; break;
			case 72: s = "invalid AddOp"; break;
			case 73: s = "invalid Factor"; break;
			case 74: s = "invalid MulOp"; break;
			case 75: s = "invalid Primary"; break;
			case 76: s = "invalid Primary"; break;

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
