
using System;
using System.IO;
using System.Collections;
using System.Text;

namespace Parva {

public class Token {
	public int kind;    // token kind
	public int pos;     // token position in the source text (starting at 0)
	public int col;     // token column (starting at 0)
	public int line;    // token line (starting at 1)
	public string val;  // token value
	public Token next;  // AW 2003-03-07 Tokens are kept in linked list
}

public class Buffer {
	public const char EOF = (char)256;
	static byte[] buf;
	static int bufLen;
	static int pos;

	public static void Fill (Stream s) {
		bufLen = (int) s.Length;
		buf = new byte[bufLen];
		s.Read(buf, 0, bufLen);
		pos = 0;
	}

	public static int Read () {
		if (pos < bufLen) return buf[pos++];
		else return EOF;                          /* pdt */
	}

	public static int Peek () {
		if (pos < bufLen) return buf[pos];
		else return EOF;                          /* pdt */
	}

	/* AW 2003-03-10 moved this from ParserGen.cs */
	public static string GetString (int beg, int end) {
		StringBuilder s = new StringBuilder(64);
		int oldPos = Buffer.Pos;
		Buffer.Pos = beg;
		while (beg < end) { s.Append((char)Buffer.Read()); beg++; }
		Buffer.Pos = oldPos;
		return s.ToString();
	}

	public static int Pos {
		get { return pos; }
		set {
			if (value < 0) pos = 0;
			else if (value >= bufLen) pos = bufLen;
			else pos = value;
		}
	}

} // end Buffer

public class Scanner {
	const char EOL = '\n';
	const int  eofSym = 0;
	const int charSetSize = 256;
	const int maxT = 59;
	const int noSym = 59;
	static short[] start = {
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  0, 48,  4,  0, 19, 39, 35,  7, 23, 24, 37, 31, 25, 32,  0, 38,
	  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 47, 28, 50, 49, 51,  0,
	  0,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,
	  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, 46,  0, 30,  0,  0,
	  0,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,
	  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, 26, 33, 27,  0,  0,
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
	  -1};


	static Token t;          // current token
	static char ch;          // current input character
	static int pos;          // column number of current character
	static int line;         // line number of current character
	static int lineStart;    // start position of current line
	static int oldEols;      // EOLs that appeared in a comment;
	static BitArray ignore;  // set of characters to be ignored by the scanner

	static Token tokens;     // the complete input token stream
	static Token pt;         // current peek token

	public static void Init (string fileName) {
		FileStream s = null;
		try {
			s = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			Init(s);
		} catch (IOException) {
			Console.WriteLine("--- Cannot open file {0}", fileName);
			System.Environment.Exit(1);
		} finally {
			if (s != null) s.Close();
		}
	}

	public static void Init (Stream s) {
		Buffer.Fill(s);
		pos = -1; line = 1; lineStart = 0;
		oldEols = 0;
		NextCh();
		ignore = new BitArray(charSetSize+1);
		ignore[' '] = true;  // blanks are always white space
		ignore[9] = true; ignore[10] = true; ignore[11] = true; ignore[12] = true; 
		ignore[13] = true; 
		//--- AW: fill token list
		tokens = new Token();  // first token is a dummy
		Token node = tokens;
		do {
			node.next = NextToken();
			node = node.next;
		} while (node.kind != eofSym);
		node.next = node;
		node.val = "EOF";
		t = pt = tokens;
	}

	static void NextCh() {
		if (oldEols > 0) { ch = EOL; oldEols--; }
		else {
			ch = (char)Buffer.Read(); pos++;
			// replace isolated '\r' by '\n' in order to make
			// eol handling uniform across Windows, Unix and Mac
			if (ch == '\r' && Buffer.Peek() != '\n') ch = EOL;
			if (ch == EOL) { line++; lineStart = pos + 1; }
		}

	}


	static bool Comment0() {
		int level = 1, line0 = line, lineStart0 = lineStart;
		NextCh();
		if (ch == '*') {
			NextCh();
			for(;;) {
				if (ch == '*') {
					NextCh();
					if (ch == '/') {
						level--;
						if (level == 0) { oldEols = line - line0; NextCh(); return true; }
						NextCh();
					}
				} else if (ch == Buffer.EOF) return false;
				else NextCh();
			}
		} else {
			if (ch == EOL) { line--; lineStart = lineStart0; }
			pos = pos - 2; Buffer.Pos = pos+1; NextCh();
		}
		return false;
	}

	static bool Comment1() {
		int level = 1, line0 = line, lineStart0 = lineStart;
		NextCh();
		if (ch == '/') {
			NextCh();
			for(;;) {
				if (ch == 10) {
					level--;
					if (level == 0) { oldEols = line - line0; NextCh(); return true; }
					NextCh();
				} else if (ch == Buffer.EOF) return false;
				else NextCh();
			}
		} else {
			if (ch == EOL) { line--; lineStart = lineStart0; }
			pos = pos - 2; Buffer.Pos = pos+1; NextCh();
		}
		return false;
	}


	static void CheckLiteral() {
		switch (t.val) {
			case "void": t.kind = 5; break;
			case "break": t.kind = 12; break;
			case "const": t.kind = 13; break;
			case "true": t.kind = 14; break;
			case "false": t.kind = 15; break;
			case "null": t.kind = 16; break;
			case "int": t.kind = 18; break;
			case "bool": t.kind = 19; break;
			case "char": t.kind = 20; break;
			case "if": t.kind = 23; break;
			case "then": t.kind = 24; break;
			case "elsif": t.kind = 25; break;
			case "else": t.kind = 26; break;
			case "while": t.kind = 27; break;
			case "do": t.kind = 28; break;
			case "repeat": t.kind = 29; break;
			case "until": t.kind = 30; break;
			case "switch": t.kind = 31; break;
			case "default": t.kind = 32; break;
			case "case": t.kind = 34; break;
			case "halt": t.kind = 37; break;
			case "return": t.kind = 38; break;
			case "read": t.kind = 39; break;
			case "readLine": t.kind = 40; break;
			case "write": t.kind = 41; break;
			case "writeLine": t.kind = 42; break;
			case "new": t.kind = 46; break;
			default: break;
		}
	}

	/* AW Scan() renamed to NextToken() */
	static Token NextToken() {
		while (ignore[ch]) NextCh();
		if (ch == '/' && Comment0() ||ch == '/' && Comment1()) return NextToken();
		t = new Token();
		t.pos = pos; t.col = pos - lineStart + 1; t.line = line;
		int state = start[ch];
		StringBuilder buf = new StringBuilder(16);
		buf.Append(ch); NextCh();
		switch (state) {
			case -1: { t.kind = eofSym; goto done; } // NextCh already done /* pdt */
			case 0: { t.kind = noSym; goto done; }   // NextCh already done
			case 1:
				if ((ch >= '0' && ch <= '9'
				  || ch >= 'A' && ch <= 'Z'
				  || ch >= 'a' && ch <= 'z')) { buf.Append(ch); NextCh(); goto case 1; }
				else if (ch == '_') { buf.Append(ch); NextCh(); goto case 2; }
				else { t.kind = 1; t.val = buf.ToString(); CheckLiteral(); return t; }
			case 2:
				if ((ch >= '0' && ch <= '9'
				  || ch >= 'A' && ch <= 'Z'
				  || ch >= 'a' && ch <= 'z')) { buf.Append(ch); NextCh(); goto case 1; }
				else if (ch == '_') { buf.Append(ch); NextCh(); goto case 2; }
				else { t.kind = noSym; goto done; }
			case 3:
				if ((ch >= '0' && ch <= '9')) { buf.Append(ch); NextCh(); goto case 3; }
				else { t.kind = 2; goto done; }
			case 4:
				if ((ch >= ' ' && ch <= '!'
				  || ch >= '#' && ch <= '['
				  || ch >= ']' && ch <= 255)) { buf.Append(ch); NextCh(); goto case 4; }
				else if ((ch == 92)) { buf.Append(ch); NextCh(); goto case 5; }
				else if (ch == '"') { buf.Append(ch); NextCh(); goto case 6; }
				else { t.kind = noSym; goto done; }
			case 5:
				if ((ch >= ' ' && ch <= 255)) { buf.Append(ch); NextCh(); goto case 4; }
				else { t.kind = noSym; goto done; }
			case 6:
				{ t.kind = 3; goto done; }
			case 7:
				if ((ch >= ' ' && ch <= '&'
				  || ch >= '(' && ch <= '['
				  || ch >= ']' && ch <= 255)) { buf.Append(ch); NextCh(); goto case 8; }
				else if ((ch == 92)) { buf.Append(ch); NextCh(); goto case 9; }
				else { t.kind = noSym; goto done; }
			case 8:
				if (ch == 39) { buf.Append(ch); NextCh(); goto case 10; }
				else { t.kind = noSym; goto done; }
			case 9:
				if ((ch >= ' ' && ch <= 255)) { buf.Append(ch); NextCh(); goto case 8; }
				else { t.kind = noSym; goto done; }
			case 10:
				{ t.kind = 4; goto done; }
			case 11:
				{ t.kind = 60; goto done; }
			case 12:
				{ t.kind = 61; goto done; }
			case 13:
				{ t.kind = 62; goto done; }
			case 14:
				{ t.kind = 63; goto done; }
			case 15:
				{ t.kind = 64; goto done; }
			case 16:
				if (ch == 'D') { buf.Append(ch); NextCh(); goto case 17; }
				else { t.kind = noSym; goto done; }
			case 17:
				{ t.kind = 65; goto done; }
			case 18:
				{ t.kind = 66; goto done; }
			case 19:
				if (ch == 'C') { buf.Append(ch); NextCh(); goto case 20; }
				else if (ch == 'D') { buf.Append(ch); NextCh(); goto case 21; }
				else if (ch == 'S') { buf.Append(ch); NextCh(); goto case 22; }
				else if (ch == 'H') { buf.Append(ch); NextCh(); goto case 16; }
				else { t.kind = noSym; goto done; }
			case 20:
				if (ch == '+') { buf.Append(ch); NextCh(); goto case 11; }
				else if (ch == '-') { buf.Append(ch); NextCh(); goto case 12; }
				else { t.kind = noSym; goto done; }
			case 21:
				if (ch == '+') { buf.Append(ch); NextCh(); goto case 13; }
				else if (ch == '-') { buf.Append(ch); NextCh(); goto case 14; }
				else { t.kind = noSym; goto done; }
			case 22:
				if (ch == 'D') { buf.Append(ch); NextCh(); goto case 15; }
				else if (ch == 'T') { buf.Append(ch); NextCh(); goto case 18; }
				else { t.kind = noSym; goto done; }
			case 23:
				{ t.kind = 6; goto done; }
			case 24:
				{ t.kind = 7; goto done; }
			case 25:
				{ t.kind = 8; goto done; }
			case 26:
				{ t.kind = 9; goto done; }
			case 27:
				{ t.kind = 10; goto done; }
			case 28:
				{ t.kind = 11; goto done; }
			case 29:
				{ t.kind = 17; goto done; }
			case 30:
				{ t.kind = 22; goto done; }
			case 31:
				{ t.kind = 35; goto done; }
			case 32:
				{ t.kind = 36; goto done; }
			case 33:
				if (ch == '|') { buf.Append(ch); NextCh(); goto case 34; }
				else { t.kind = noSym; goto done; }
			case 34:
				{ t.kind = 43; goto done; }
			case 35:
				if (ch == '&') { buf.Append(ch); NextCh(); goto case 36; }
				else { t.kind = noSym; goto done; }
			case 36:
				{ t.kind = 44; goto done; }
			case 37:
				{ t.kind = 47; goto done; }
			case 38:
				{ t.kind = 48; goto done; }
			case 39:
				{ t.kind = 49; goto done; }
			case 40:
				{ t.kind = 50; goto done; }
			case 41:
				{ t.kind = 51; goto done; }
			case 42:
				{ t.kind = 53; goto done; }
			case 43:
				{ t.kind = 55; goto done; }
			case 44:
				{ t.kind = 57; goto done; }
			case 45:
				{ t.kind = 58; goto done; }
			case 46:
				if (ch == ']') { buf.Append(ch); NextCh(); goto case 29; }
				else { t.kind = 21; goto done; }
			case 47:
				if (ch == '=') { buf.Append(ch); NextCh(); goto case 45; }
				else { t.kind = 33; goto done; }
			case 48:
				if (ch == '=') { buf.Append(ch); NextCh(); goto case 41; }
				else { t.kind = 45; goto done; }
			case 49:
				if (ch == '=') { buf.Append(ch); NextCh(); goto case 40; }
				else { t.kind = 52; goto done; }
			case 50:
				if (ch == '>') { buf.Append(ch); NextCh(); goto case 42; }
				else if (ch == '=') { buf.Append(ch); NextCh(); goto case 43; }
				else { t.kind = 54; goto done; }
			case 51:
				if (ch == '=') { buf.Append(ch); NextCh(); goto case 44; }
				else { t.kind = 56; goto done; }

		}
		done:
		t.val = buf.ToString();
		return t;
	}

	/* AW 2003-03-07 get the next token, move on and synch peek token with current */
	public static Token Scan () {
		t = pt = t.next;
		return t;
	}

	/* AW 2003-03-07 get the next token, ignore pragmas */
	public static Token Peek () {
		do {                      // skip pragmas while peeking
			pt = pt.next;
		} while (pt.kind > maxT);
		return pt;
	}

	/* AW 2003-03-11 to make sure peek start at current scan position */
	public static void ResetPeek () { pt = t; }

} // end Scanner

} // end namespace
