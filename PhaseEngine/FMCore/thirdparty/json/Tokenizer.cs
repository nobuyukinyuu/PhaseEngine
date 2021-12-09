using System;
using System.Diagnostics;
using GdsFMJson;
using static System.Globalization.CultureInfo;

namespace GdsFMJson
{

public class JSONToken {
	public const int TOKEN_UNKNOWN = -1;
	public const int TOKEN_COMMA = 0;
	public const int TOKEN_OPEN_CURLY = 1;
	public const int TOKEN_CLOSE_CURLY = 2;
	public const int TOKEN_OPEN_SQUARE = 3;
	public const int TOKEN_CLOSE_SQUARE = 4;
	public const int TOKEN_COLON = 6;
	public const int TOKEN_TRUE = 7;
	public const int TOKEN_FALSE = 8;
	public const int TOKEN_NULL = 9;
	public const int TOKEN_STRING = 10;
	public const int TOKEN_FLOAT = 11;
	public const int TOKEN_UNPARSED_FLOAT = 12;
	public const int TOKEN_INTEGER = 13;

	public int tokenType;
	public object value;

	// Private

	static JSONToken reusableToken = new JSONToken(-1, null);
    
	JSONToken( int tokenType, object value) {
		this.tokenType = tokenType;
		this.value = value;
	}

    // Public
    
    public static JSONToken CreateToken(int tokenType, float value){
		reusableToken.tokenType = tokenType;
		reusableToken.value = value;
		return reusableToken;
	}
	
	public static JSONToken CreateToken( int tokenType, int value ) {
		reusableToken.tokenType = tokenType;
		reusableToken.value = value;
		return reusableToken;
	}
	
	public static JSONToken CreateToken( int tokenType, string value ) {
		reusableToken.tokenType = tokenType;
		reusableToken.value = value;
		return reusableToken;
	}
	
	public static JSONToken CreateToken( int tokenType, object value ) {
		reusableToken.tokenType = tokenType;
		reusableToken.value = value;
		return reusableToken;
	}
    
	public override string ToString(){
		return "JSONToken - type: " + tokenType + ", value: " + GetValueString();
	}

	public string GetValueString() {
		switch (tokenType) {
			case TOKEN_FLOAT:
				return Convert.ToString(this.value, InvariantCulture);
			case TOKEN_INTEGER:
				return Convert.ToString(this.value, InvariantCulture);
			case TOKEN_NULL:
				return "NULL";
			default:
                return Convert.ToString(this.value, InvariantCulture) ?? "null";
		}	
	}
	
	
}

public class JSONTokeniser {

	// Private

	string jsonString;
	int stringIndex;
	int character;
	bool silent;

	// Public

	public JSONTokeniser( string jsonString, bool silent = false ){
		this.silent = silent;
		this.jsonString = jsonString;
		NextChar();
	}

	public string GetCurrentSectionString(int backwards=20,int forwards=20){
		// return "Section: " + jsonString[Math.Max(stringIndex-backwards,0)..Math.Min(stringIndex+forwards,jsonString.Length)];
		return "Section: " + jsonString.Slice(Math.Max(stringIndex-backwards,0), Math.Min(stringIndex+forwards,jsonString.Length));
		// return "Section: " + jsonString.Substring(Math.Max(stringIndex-backwards,0), Math.Min(stringIndex+forwards, jsonString.Length));
	}

	public JSONToken NextToken(){
		JSONToken retToken=null;
		SkipIgnored();

        switch (character){
			case ASCIICodes.CHR_OPEN_CURLY:
				retToken = JSONToken.CreateToken(JSONToken.TOKEN_OPEN_CURLY,"{");
                break;
			case ASCIICodes.CHR_CLOSE_CURLY:
				retToken = JSONToken.CreateToken(JSONToken.TOKEN_CLOSE_CURLY,"}");
                break;
			case ASCIICodes.CHR_OPEN_SQUARE:
				retToken = JSONToken.CreateToken(JSONToken.TOKEN_OPEN_SQUARE,"[");
                break;
			case ASCIICodes.CHR_CLOSE_SQUARE:
				retToken = JSONToken.CreateToken(JSONToken.TOKEN_CLOSE_SQUARE,"]");
                break;
			case ASCIICodes.CHR_COMMA:
				retToken = JSONToken.CreateToken(JSONToken.TOKEN_COMMA,",");
                break;
			case ASCIICodes.CHR_COLON:
				retToken = JSONToken.CreateToken(JSONToken.TOKEN_COLON,":");
                break;
			case ASCIICodes.CHR_LOWER_T:
				if (String.Compare(jsonString.Slice(stringIndex,stringIndex+3), "rue") == 0){
					stringIndex += 3;
					retToken = JSONToken.CreateToken(JSONToken.TOKEN_TRUE,"true");
				}
                break;
			case ASCIICodes.CHR_LOWER_F:
				if (String.Compare(jsonString.Slice(stringIndex,stringIndex+4), "alse") == 0){
					stringIndex += 4;
					retToken = JSONToken.CreateToken(JSONToken.TOKEN_FALSE,"false");
				}
                break;
			case ASCIICodes.CHR_LOWER_N:
				if (String.Compare(jsonString.Slice(stringIndex,stringIndex+3),"ull") == 0){
					stringIndex += 3;
					retToken = JSONToken.CreateToken(JSONToken.TOKEN_NULL,"null");
				}
                break;
			case ASCIICodes.CHR_DOUBLE_QUOTE:
				int startIndex = stringIndex;
        		int endIndex = jsonString.IndexOf("\"",stringIndex);
                while (endIndex != -1 && jsonString[endIndex-1] == ASCIICodes.CHR_BACKSLASH){
                    endIndex = jsonString.IndexOf("\"",endIndex+1);
                }
                if (endIndex == -1){
                    ParseFailure("Unterminated string");
                }
				
                retToken = JSONToken.CreateToken(JSONToken.TOKEN_STRING,jsonString.Slice(startIndex,endIndex));
				stringIndex = endIndex+1;
                break;
						
			default:
				// Is it a Number?
				if (character == ASCIICodes.CHR_HYPHEN || IsDigit(character)){
					return ParseNumberToken(character); // We return here because ParseNumberToken moves the token pointer forward
				} else if (character == ASCIICodes.CHR_NUL) {
					retToken = null; // End of string so just leave// 
				}
                break;								
		}

		if (retToken == null){
			ParseFailure("Unknown token, character: " + character.ToString());
			retToken = JSONToken.CreateToken(JSONToken.TOKEN_UNKNOWN,null);
		} else {
			NextChar();
		}
		return retToken;

	}

	// Private
	
	int NextChar(){
		if (stringIndex >= jsonString.Length){
			character = ASCIICodes.CHR_NUL;
            return character;
		}
		character = jsonString[stringIndex];
		stringIndex += 1;
		return character;
	}

	JSONToken ParseNumberToken(int firstChar){
		int index = stringIndex-1;
		// First just get the full string
		while (character != ASCIICodes.CHR_SPACE && character != ASCIICodes.CHR_COMMA && 
               character != ASCIICodes.CHR_CLOSE_CURLY && character != ASCIICodes.CHR_CLOSE_SQUARE && character != ASCIICodes.CHR_NUL){
			NextChar();
		}
		if (character == ASCIICodes.CHR_NUL){
			ParseFailure("Unterminated Number");
			return JSONToken.CreateToken(JSONToken.TOKEN_UNKNOWN,null);
		}

		string numberString = jsonString.Slice(index,stringIndex-1);
		
		if (numberString.IndexOf(".") != -1 || numberString.IndexOf("e") != -1 || numberString.IndexOf("E") != -1){
		    return JSONToken.CreateToken(JSONToken.TOKEN_UNPARSED_FLOAT,numberString);
		} else {
			int value = ParseInteger(numberString);
			return JSONToken.CreateToken(JSONToken.TOKEN_INTEGER,value);
		} 
	}

	// No error trapping or anything like that
	int ParseInteger(string str){
		// return (int)(str);
		return Convert.ToInt32(str);
	}

	// No error trapping or anything like that
	float ParseFloat(string str){
        // return Float(str)
        return Convert.ToSingle(str);
    }

	bool IsDigit(int character){
		return (character >= 48 && character <= 58 );
	}

	void SkipIgnored(){
		int ignoredCount;
		do {
			ignoredCount = 0;
			ignoredCount += SkipWhitespace();
			ignoredCount += SkipComments();
        } while (ignoredCount != 0);
	}

	int SkipWhitespace(){
		int index = stringIndex;
		while (character <= ASCIICodes.CHR_SPACE && character != ASCIICodes.CHR_NUL){
			NextChar();
		}
		return stringIndex-index;
	}

	int SkipComments(){
		int index = stringIndex;
		if (character == ASCIICodes.CHR_FORWARD_SLASH){
			NextChar();
			if (character == ASCIICodes.CHR_FORWARD_SLASH){
				while (character != ASCIICodes.CHR_CR && character != ASCIICodes.CHR_LF && character != ASCIICodes.CHR_NUL){
					NextChar();
				}
			} else if (character == ASCIICodes.CHR_ASTERISK) {
				while(true){
					NextChar();
                    if (character == ASCIICodes.CHR_ASTERISK){
						NextChar();
                        if (character == ASCIICodes.CHR_FORWARD_SLASH){
							break;
						}
					}
					if (character == ASCIICodes.CHR_NUL){
						ParseFailure("Unterminated comment");
						break;
					}
				}
			} else {
				ParseFailure("Unrecognised comment opening");
			}
			NextChar();
		}
		return stringIndex-index;
	}

	void ParseFailure(string description) {
		if (silent) 	return;
		
		Debug.Print("JSON parse error at index: " + stringIndex);
		Debug.Print(description);
		Debug.Print(GetCurrentSectionString());
		stringIndex = jsonString.Length;
	}
}

class ASCIICodes {
    public const int CHR_NUL = 0;       //  null characteracter
    public const int CHR_SOH = 1;       //  Start of Heading
    public const int CHR_STX = 2;       //  Start of Text
    public const int CHR_ETX = 3;       //  } of Text
    public const int CHR_EOT = 4;       //  } of Transmission
    public const int CHR_ENQ = 5;       //  Enquiry
    public const int CHR_ACK = 6;       //  Acknowledgment
    public const int CHR_BEL = 7;       //  Bell
    public const int CHR_BACKSPACE = 8; //  Backspace
    public const int CHR_TAB = 9;       //  Horizontal tab
    public const int CHR_LF = 10;       //  Linefeed
    public const int CHR_VTAB = 11;     //  Vertical tab
    public const int CHR_FF = 12;       //  Form feed
    public const int CHR_CR = 13;       //  Carriage return
    public const int CHR_SO = 14;       //  Shift Out
    public const int CHR_SI = 15;       //  Shift In
    public const int CHR_DLE = 16;      //  Data Line Escape
    public const int CHR_DC1 = 17;      //  Device Control 1;
    public const int CHR_DC2 = 18;      //  Device Control 2;
    public const int CHR_DC3 = 19;      //  Device Control 3;
    public const int CHR_DC4 = 20;      //  Device Control 4;
    public const int CHR_NAK = 21;      //  Negative Acknowledgment
    public const int CHR_SYN = 22;      //  Synchronous Idle
    public const int CHR_ETB = 23;      //  } of Transmit Block
    public const int CHR_CAN = 24;      //  Cancel
    public const int CHR_EM = 25;       //  } of Medium
    public const int CHR_SUB = 26;      //  Substitute
    public const int CHR_ESCAPE = 27;   //  Escape
    public const int CHR_FS = 28;       //  File separator
    public const int CHR_GS = 29;       //  Group separator
    public const int CHR_RS = 30;       //  Record separator
    public const int CHR_US = 31;       //  Unit separator
    
    //  visible characteracters
    public const int CHR_SPACE = 32;                //
    public const int CHR_EXCLAMATION = 33;          // !
    public const int CHR_DOUBLE_QUOTE = 34;         // "
    public const int CHR_HASH = 35;                 // #
    public const int CHR_DOLLAR = 36;               // $
    public const int CHR_PERCENT = 37;              // %
    public const int CHR_AMPERSAND = 38;            // &
    public const int CHR_SINGLE_QUOTE = 39;         // '
    public const int CHR_OPEN_ROUND = 40;           // (
    public const int CHR_CLOSE_ROUND = 41;          // )
    public const int CHR_ASTERISK = 42;             // *
    public const int CHR_PLUS = 43;                 // +
    public const int CHR_COMMA = 44;                // ,
    public const int CHR_HYPHEN = 45;               // -
    public const int CHR_PERIOD = 46;               // .
    public const int CHR_FORWARD_SLASH = 47;        // /
    public const int CHR_0 = 48;
    public const int CHR_1 = 49;
    public const int CHR_2 = 50;
    public const int CHR_3 = 51;
    public const int CHR_4 = 52;
    public const int CHR_5 = 53;
    public const int CHR_6 = 54;
    public const int CHR_7 = 55;
    public const int CHR_8 = 56;
    public const int CHR_9 = 57;
    public const int CHR_COLON = 58;        // :
    public const int CHR_SEMICOLON = 59;    // ;
    public const int CHR_LESS_THAN = 60;    // <
    public const int CHR_EQUALS = 61;       // =
    public const int CHR_GREATER_THAN = 62; // >
    public const int CHR_QUESTION = 63;     // ?
    public const int CHR_AT = 64;           // @
    public const int CHR_UPPER_A = 65;
    public const int CHR_UPPER_B = 66;
    public const int CHR_UPPER_C = 67;
    public const int CHR_UPPER_D = 68;
    public const int CHR_UPPER_E = 69;
    public const int CHR_UPPER_F = 70;
    public const int CHR_UPPER_G = 71;
    public const int CHR_UPPER_H = 72;
    public const int CHR_UPPER_I = 73;
    public const int CHR_UPPER_J = 74;
    public const int CHR_UPPER_K = 75;
    public const int CHR_UPPER_L = 76;
    public const int CHR_UPPER_M = 77;
    public const int CHR_UPPER_N = 78;
    public const int CHR_UPPER_O = 79;
    public const int CHR_UPPER_P = 80;
    public const int CHR_UPPER_Q = 81;
    public const int CHR_UPPER_R = 82;
    public const int CHR_UPPER_S = 83;
    public const int CHR_UPPER_T = 84;
    public const int CHR_UPPER_U = 85;
    public const int CHR_UPPER_V = 86;
    public const int CHR_UPPER_W = 87;
    public const int CHR_UPPER_X = 88;
    public const int CHR_UPPER_Y = 89;
    public const int CHR_UPPER_Z = 90;
    public const int CHR_OPEN_SQUARE = 91;     // [
    public const int CHR_BACKSLASH = 92;        // \
    public const int CHR_CLOSE_SQUARE = 93;    // ]
    public const int CHR_CIRCUMFLEX = 94;       // ^
    public const int CHR_UNDERSCORE = 95;       // _
    public const int CHR_BACKTICK = 96;         // `
    public const int CHR_LOWER_A = 97;
    public const int CHR_LOWER_B = 98;
    public const int CHR_LOWER_C = 99;
    public const int CHR_LOWER_D = 100;
    public const int CHR_LOWER_E = 101;
    public const int CHR_LOWER_F = 102;
    public const int CHR_LOWER_G = 103;
    public const int CHR_LOWER_H = 104;
    public const int CHR_LOWER_I = 105;
    public const int CHR_LOWER_J = 106;
    public const int CHR_LOWER_K = 107;
    public const int CHR_LOWER_L = 108;
    public const int CHR_LOWER_M = 109;
    public const int CHR_LOWER_N = 110;
    public const int CHR_LOWER_O = 111;
    public const int CHR_LOWER_P = 112;
    public const int CHR_LOWER_Q = 113;
    public const int CHR_LOWER_R = 114;
    public const int CHR_LOWER_S = 115;
    public const int CHR_LOWER_T = 116;
    public const int CHR_LOWER_U = 117;
    public const int CHR_LOWER_V = 118;
    public const int CHR_LOWER_W = 119;
    public const int CHR_LOWER_X = 120;
    public const int CHR_LOWER_Y = 121;
    public const int CHR_LOWER_Z = 122;
    public const int CHR_OPEN_CURLY = 123;  // {
    public const int CHR_PIPE = 124;        // |
    public const int CHR_CLOSE_CURLY = 125; // }
    public const int CHR_TILDE = 126;       // ~
    public const int CHR_DELETE = 127;

}

}  //End Namespace