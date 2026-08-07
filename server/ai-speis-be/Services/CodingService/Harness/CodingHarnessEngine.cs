using System;
using System.Text;

namespace ai_speis_be.Services.CodingService.Harness
{
    public class CodingHarnessEngine : ICodingHarnessEngine
    {
        public string WrapCode(string sourceCode, int languageId, string functionName)
        {
            if (string.IsNullOrWhiteSpace(sourceCode))
                return sourceCode ?? string.Empty;

            string fnName = string.IsNullOrWhiteSpace(functionName) ? "solution" : functionName.Trim();

            return languageId switch
            {
                // Python: 70 (Python 2), 71 (Python 3)
                70 or 71 => WrapPython(sourceCode, fnName),

                // JavaScript / Node.js: 63, 93
                63 or 93 => WrapJavaScript(sourceCode, fnName),

                // Java: 62, 91
                62 or 91 => WrapJava(sourceCode, fnName),

                // C#: 51, 92
                51 or 92 => WrapCsharp(sourceCode, fnName),

                // C++: 52, 53, 54, 76
                52 or 53 or 54 or 76 => WrapCpp(sourceCode, fnName),

                // C: 48, 49, 50, 75
                48 or 49 or 50 or 75 => WrapC(sourceCode, fnName),

                // Fallback for unknown language IDs
                _ => WrapFallback(sourceCode, languageId, fnName)
            };
        }

        private static string WrapFallback(string sourceCode, int languageId, string fnName)
        {
            if (sourceCode.Contains("function ") || sourceCode.Contains("const ") || sourceCode.Contains("let "))
                return WrapJavaScript(sourceCode, fnName);
            if (sourceCode.Contains("#include") && (sourceCode.Contains("char**") || sourceCode.Contains("char *")))
                return WrapC(sourceCode, fnName);
            if (sourceCode.Contains("def "))
                return WrapPython(sourceCode, fnName);

            return sourceCode;
        }

        private static string WrapPython(string code, string fnName)
        {
            if (code.Contains("__main__") || code.Contains("sys.stdin"))
                return code;

            var harness = $$"""

# --- AUTOMATIC TEST HARNESS ---
if __name__ == '__main__':
    import sys, json
    __raw_input = sys.stdin.read().strip()
    if __raw_input:
        try:
            __data = json.loads(__raw_input)
            __fn = globals().get('{{fnName}}') or globals().get('config_change_plan') or globals().get('solution')
            if not __fn:
                for __k, __v in list(globals().items()):
                    if callable(__v) and not __k.startswith('_') and __k not in ('json', 'sys'):
                        __fn = __v
                        break
            if __fn:
                if isinstance(__data, dict):
                    if 'current' in __data and 'desired' in __data:
                        __res = __fn(__data['current'], __data['desired'])
                    else:
                        __res = __fn(**__data)
                elif isinstance(__data, list):
                    __res = __fn(*__data)
                else:
                    __res = __fn(__raw_input)
                
                if isinstance(__res, (dict, list)):
                    print(json.dumps(__res, separators=(',', ':')))
                else:
                    print(__res)
            else:
                sys.stderr.write("No valid Python function found.\n")
                sys.exit(1)
        except Exception as __e:
            import traceback
            sys.stderr.write(str(__e) + '\n' + traceback.format_exc())
            sys.exit(1)
""";
            return code + "\n" + harness;
        }

        private static string WrapJavaScript(string code, string fnName)
        {
            if (code.Contains("process.stdin") || code.Contains("readFileSync"))
                return code;

            var harness = $$"""

// --- AUTOMATIC TEST HARNESS ---
if (typeof process !== 'undefined') {
  const __run = () => {
    const fs = require('fs');
    const inputStr = (fs.readFileSync(0, 'utf-8') || '').trim();
    if (!inputStr) return;

    let data;
    try { data = JSON.parse(inputStr); } catch (e) { data = inputStr; }

    // Find target function without eval (faster startup)
    let fn = (typeof {{fnName}} === 'function') ? {{fnName}}
           : (typeof solution === 'function') ? solution
           : (typeof config_change_plan === 'function') ? config_change_plan
           : null;
    // Fallback: search globals for any user-defined function
    if (!fn) {
      const skip = new Set(['require','parseInt','parseFloat','isNaN','isFinite','decodeURI',
        'decodeURIComponent','encodeURI','encodeURIComponent','escape','unescape','eval']);
      for (const k of Object.getOwnPropertyNames(global)) {
        if (!skip.has(k) && typeof global[k] === 'function' && !k.startsWith('_')) {
          fn = global[k]; break;
        }
      }
    }

    if (!fn) { process.stderr.write("No valid JavaScript function found.\n"); process.exit(1); }

    let res;
    if (Array.isArray(data)) {
      res = fn(...data);
    } else if (typeof data === 'object' && data !== null) {
      if ('current' in data && 'desired' in data) {
        res = fn(data.current, data.desired);
      } else {
        res = fn(...Object.values(data));
      }
    } else {
      res = fn(data);
    }

    // Handle async/Promise results
    if (res && typeof res.then === 'function') {
      res.then(v => { if (v !== undefined) console.log(typeof v === 'object' ? JSON.stringify(v) : String(v)); })
         .catch(e => { process.stderr.write(String(e) + '\n'); process.exit(1); });
    } else if (res !== undefined) {
      console.log(typeof res === 'object' ? JSON.stringify(res) : String(res));
    }
  };
  try { __run(); } catch (err) {
    process.stderr.write((err && err.stack ? err.stack : String(err)) + '\n');
    process.exit(1);
  }
}
""";
            return code + "\n" + harness;
        }

        private static string WrapJava(string code, string fnName)
        {
            if (code.Contains("public static void main"))
                return code;

            // Remove 'public' from 'public class Solution' to allow Main.java to compile multiple classes
            string cleanCode = code.Replace("public class Solution", "class Solution");

            var harness = $$"""

// --- AUTOMATIC TEST HARNESS ---
class Main {
    public static void main(String[] args) {
        try {
            java.util.Scanner sc = new java.util.Scanner(System.in);
            if (!sc.hasNextLine()) return;
            StringBuilder sb = new StringBuilder();
            while (sc.hasNextLine()) {
                sb.append(sc.nextLine()).append("\n");
            }
            String rawInput = sb.toString().trim();
            if (rawInput.isEmpty()) return;

            Class<?> solClass = Class.forName("Solution");
            Object solInstance = solClass.getDeclaredConstructor().newInstance();

            java.lang.reflect.Method targetMethod = null;
            for (java.lang.reflect.Method m : solClass.getDeclaredMethods()) {
                if (m.getName().equalsIgnoreCase("{{fnName}}") || m.getName().equalsIgnoreCase("config_change_plan")) {
                    targetMethod = m;
                    break;
                }
            }
            if (targetMethod == null && solClass.getDeclaredMethods().length > 0) {
                targetMethod = solClass.getDeclaredMethods()[0];
            }
            if (targetMethod == null) return;

            Class<?>[] paramTypes = targetMethod.getParameterTypes();
            Object[] invokeArgs = parseJavaArgs(rawInput, paramTypes);

            targetMethod.setAccessible(true);
            Object result = targetMethod.invoke(solInstance, invokeArgs);

            if (result != null) {
                if (result.getClass().isArray()) {
                    System.out.println(formatJavaArray(result));
                } else {
                    System.out.println(result);
                }
            }
        } catch (Throwable t) {
            t.printStackTrace(System.err);
            System.exit(1);
        }
    }

    private static Object[] parseJavaArgs(String input, Class<?>[] types) {
        if (types.length == 0) return new Object[0];
        Object[] args = new Object[types.length];
        java.util.List<String> tokens = splitJsonTokens(input);
        for (int i = 0; i < types.length; i++) {
            String token = i < tokens.size() ? tokens.get(i) : input;
            args[i] = parseSingleArg(token, types[i]);
        }
        return args;
    }

    private static java.util.List<String> splitJsonTokens(String input) {
        java.util.List<String> list = new java.util.ArrayList<>();
        if (input.startsWith("[") && input.endsWith("]")) {
            String inner = input.substring(1, input.length() - 1).trim();
            int depth = 0;
            boolean inString = false;
            StringBuilder current = new StringBuilder();
            for (int i = 0; i < inner.length(); i++) {
                char c = inner.charAt(i);
                if (c == '"' && (i == 0 || inner.charAt(i - 1) != '\\')) inString = !inString;
                if (!inString) {
                    if (c == '[' || c == '{') depth++;
                    else if (c == ']' || c == '}') depth--;
                    else if (c == ',' && depth == 0) {
                        list.add(current.toString().trim());
                        current = new StringBuilder();
                        continue;
                    }
                }
                current.append(c);
            }
            if (current.length() > 0) list.add(current.toString().trim());
        } else {
            list.add(input);
        }
        return list;
    }

    private static Object parseSingleArg(String val, Class<?> type) {
        val = val.trim();
        if (val.startsWith("\"") && val.endsWith("\"")) {
            val = val.substring(1, val.length() - 1).replace("\\\"", "\"");
        }
        if (type == int.class || type == Integer.class) return Integer.parseInt(val);
        if (type == long.class || type == Long.class) return Long.parseLong(val);
        if (type == double.class || type == Double.class) return Double.parseDouble(val);
        if (type == boolean.class || type == Boolean.class) return Boolean.parseBoolean(val);
        if (type == String.class) return val;

        if (type == int[].class) {
            val = val.replaceAll("[\\[\\]]", "");
            if (val.isEmpty()) return new int[0];
            String[] parts = val.split(",");
            int[] arr = new int[parts.length];
            for (int i = 0; i < parts.length; i++) arr[i] = Integer.parseInt(parts[i].trim());
            return arr;
        }
        if (type == String[].class) {
            val = val.replaceAll("[\\[\\]]", "");
            if (val.isEmpty()) return new String[0];
            String[] parts = val.split(",");
            for (int i = 0; i < parts.length; i++) parts[i] = parts[i].trim().replaceAll("^\"|\"$", "");
            return parts;
        }
        return val;
    }

    private static String formatJavaArray(Object arr) {
        int len = java.lang.reflect.Array.getLength(arr);
        StringBuilder sb = new StringBuilder("[");
        for (int i = 0; i < len; i++) {
            sb.append(java.lang.reflect.Array.get(arr, i));
            if (i < len - 1) sb.append(",");
        }
        sb.append("]");
        return sb.toString();
    }
}
""";
            return cleanCode + "\n" + harness;
        }

        private static string WrapCsharp(string code, string fnName)
        {
            if (code.Contains("static void Main") || code.Contains("static async Task Main"))
                return code;

            var harness = $$"""

// --- AUTOMATIC TEST HARNESS ---
public class Program {
    public static void Main(string[] args) {
        try {
            string rawInput = System.Console.In.ReadToEnd().Trim();
            if (string.IsNullOrEmpty(rawInput)) return;

            System.Type solType = System.Type.GetType("Solution") ?? typeof(Solution);
            object solInstance = System.Activator.CreateInstance(solType);

            System.Reflection.MethodInfo targetMethod = null;
            foreach (System.Reflection.MethodInfo m in solType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)) {
                if (m.Name.Equals("{{fnName}}", System.StringComparison.OrdinalIgnoreCase) || m.Name.Equals("config_change_plan", System.StringComparison.OrdinalIgnoreCase)) {
                    targetMethod = m;
                    break;
                }
            }
            if (targetMethod == null) {
                System.Reflection.MethodInfo[] allM = solType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                if (allM.Length > 0) targetMethod = allM[0];
            }

            if (targetMethod == null) return;

            System.Reflection.ParameterInfo[] paramInfos = targetMethod.GetParameters();
            object[] invokeArgs = ParseCsharp2DStringArray(rawInput, paramInfos.Length);

            object result = targetMethod.Invoke(solInstance, invokeArgs);
            if (result != null) {
                if (result is System.Collections.IEnumerable && !(result is string)) {
                    System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
                    foreach (object item in (System.Collections.IEnumerable)result) {
                        list.Add(item != null ? item.ToString() : "null");
                    }
                    if (list.Count == 0) {
                        System.Console.WriteLine("[]");
                    } else {
                        System.Console.WriteLine("[\"" + string.Join("\",\"", list.ToArray()) + "\"]");
                    }
                } else {
                    System.Console.WriteLine(result.ToString());
                }
            }
        } catch (System.Exception ex) {
            System.Console.Error.WriteLine(ex.ToString());
            System.Environment.Exit(1);
        }
    }

    private static object[] ParseCsharp2DStringArray(string input, int paramCount) {
        object[] args = new object[paramCount];
        if (paramCount == 0) return args;

        int curPos = input.IndexOf("\"current\"");
        int desPos = input.IndexOf("\"desired\"");

        string[][] cur = Extract2DArray(input, curPos);
        string[][] des = Extract2DArray(input, desPos);

        if (paramCount >= 1) args[0] = cur;
        if (paramCount >= 2) args[1] = des;
        return args;
    }

    private static string[][] Extract2DArray(string str, int pos) {
        if (pos < 0) return new string[0][];
        int start = str.IndexOf('[', pos);
        if (start < 0) return new string[0][];

        System.Collections.Generic.List<string[]> list = new System.Collections.Generic.List<string[]>();
        int i = start + 1;
        while (i < str.Length && str[i] != ']') {
            if (str[i] == '[') {
                i++;
                System.Collections.Generic.List<string> row = new System.Collections.Generic.List<string>();
                while (i < str.Length && str[i] != ']') {
                    if (str[i] == '"') {
                        i++;
                        int sStart = i;
                        while (i < str.Length && str[i] != '"') i++;
                        row.Add(str.Substring(sStart, i - sStart));
                        if (i < str.Length) i++;
                    } else {
                        i++;
                    }
                }
                list.Add(row.ToArray());
            }
            i++;
        }
        return list.ToArray();
    }
}
""";
            return code + "\n" + harness;
        }

        private static string WrapCpp(string code, string fnName)
        {
            if (code.Contains("int main("))
                return code;

            var harness = $$"""

// --- AUTOMATIC TEST HARNESS ---
#include <iostream>
#include <string>
#include <vector>
#include <sstream>

static std::string __read_cpp_stdin() {
    std::string line, full;
    while (std::getline(std::cin, line)) full += line + "\n";
    return full;
}

static std::vector<std::vector<std::string>> __parse_cpp_2d_vec(const std::string& input, const std::string& key) {
    std::vector<std::vector<std::string>> res;
    size_t pos = input.find(key);
    if (pos == std::string::npos) return res;
    size_t start = input.find('[', pos);
    if (start == std::string::npos) return res;
    
    size_t i = start + 1;
    while (i < input.size() && input[i] != ']') {
        if (input[i] == '[') {
            i++;
            std::vector<std::string> pair;
            while (i < input.size() && input[i] != ']') {
                if (input[i] == '"') {
                    i++;
                    size_t strStart = i;
                    while (i < input.size() && input[i] != '"') i++;
                    pair.push_back(input.substr(strStart, i - strStart));
                    if (i < input.size()) i++;
                } else {
                    i++;
                }
            }
            res.push_back(pair);
        }
        i++;
    }
    return res;
}

int main() {
    std::string input = __read_cpp_stdin();
    if (input.empty()) return 0;

    auto cur = __parse_cpp_2d_vec(input, "\"current\"");
    auto des = __parse_cpp_2d_vec(input, "\"desired\"");

    std::vector<std::string> ans;
    Solution sol;
    ans = sol.{{fnName}}(cur, des);

    std::cout << "[";
    for (size_t i = 0; i < ans.size(); ++i) {
        std::cout << "\"" << ans[i] << "\"";
        if (i + 1 < ans.size()) std::cout << ",";
    }
    std::cout << "]" << std::endl;

    return 0;
}
""";
            return code + "\n" + harness;
        }

        private static string WrapC(string code, string fnName)
        {
            if (code.Contains("int main("))
                return code;

            var harness = $$"""

/* --- AUTOMATIC TEST HARNESS --- */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static char* __read_stdin_full() {
    size_t cap = 4096, len = 0;
    char *buf = (char*)malloc(cap);
    if (!buf) return NULL;
    int c;
    while ((c = fgetc(stdin)) != EOF) {
        if (len + 1 >= cap) {
            cap *= 2;
            buf = (char*)realloc(buf, cap);
        }
        buf[len++] = (char)c;
    }
    buf[len] = '\0';
    return buf;
}

static char*** __parse_2d_string_array(const char *str, int *outSize, int **outColSizes) {
    *outSize = 0;
    *outColSizes = NULL;
    if (!str) return NULL;

    const char *p = strchr(str, '[');
    if (!p) return NULL;
    p++;

    int cap = 16;
    char ***res = (char***)malloc(sizeof(char**) * cap);
    int *colSizes = (int*)malloc(sizeof(int) * cap);

    int count = 0;
    while (*p) {
        while (*p && (*p == ' ' || *p == ',' || *p == '\r' || *p == '\n' || *p == '\t')) p++;
        if (*p == ']' || *p == '\0') break;

        if (*p == '[') {
            p++;
            int pairCap = 4, pairCnt = 0;
            char **pair = (char**)malloc(sizeof(char*) * pairCap);
            while (*p && *p != ']') {
                while (*p && (*p == ' ' || *p == ',' || *p == '\r' || *p == '\n' || *p == '\t')) p++;
                if (*p == ']' || *p == '\0') break;

                if (*p == '"') {
                    p++;
                    const char *start = p;
                    while (*p && (*p != '"' || *(p - 1) == '\\')) p++;
                    int slen = p - start;
                    char *val = (char*)malloc(slen + 1);
                    strncpy(val, start, slen);
                    val[slen] = '\0';
                    if (*p == '"') p++;

                    if (pairCnt >= pairCap) {
                        pairCap *= 2;
                        pair = (char**)realloc(pair, sizeof(char*) * pairCap);
                    }
                    pair[pairCnt++] = val;
                } else {
                    p++;
                }
            }
            if (*p == ']') p++;

            if (count >= cap) {
                cap *= 2;
                res = (char***)realloc(res, sizeof(char**) * cap);
                colSizes = (int*)realloc(colSizes, sizeof(int) * cap);
            }
            res[count] = pair;
            colSizes[count] = pairCnt;
            count++;
        } else {
            p++;
        }
    }

    *outSize = count;
    *outColSizes = colSizes;
    return res;
}

int main() {
    char *input = __read_stdin_full();
    if (!input || strlen(input) == 0) return 0;

    const char *curPos = strstr(input, "\"current\"");
    const char *desPos = strstr(input, "\"desired\"");

    int curSize = 0, desSize = 0;
    int *curColSizes = NULL, *desColSizes = NULL;
    char ***curArr = __parse_2d_string_array(curPos, &curSize, &curColSizes);
    char ***desArr = __parse_2d_string_array(desPos, &desSize, &desColSizes);

    int returnSize = 0;
    char **ans = {{fnName}}(curArr, curSize, curColSizes, desArr, desSize, desColSizes, &returnSize);

    printf("[");
    for (int i = 0; i < returnSize; i++) {
        printf("\"%s\"", ans[i]);
        if (i < returnSize - 1) printf(",");
    }
    printf("]\n");

    return 0;
}
""";
            return code + "\n" + harness;
        }
    }
}
