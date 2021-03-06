using System;
using Wasmic.Core;
using Xunit;

namespace Wasmic.Test
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var code = "";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);
            
            Assert.Equal("(module)", actual);
        }

        [Fact]
        public void PrivateFunctionTest()
        {
            var code = @"func hoge() {}".Trim();
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module (func $hoge))", actual);
        }

        [Fact]
        public void PublicFunctionTest()
        {
            var code = @"pub func hoge() {}".Trim();
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module (func (export \"hoge\")))", actual);
        }

        [Fact]
        public void FunctionWithSingleParamTest()
        {
            var code = @"pub func hoge(a: i32) {}".Trim();
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module (func (export \"hoge\") (param $a i32)))", actual);
        }

        [Fact]
        public void FunctionWithMultipleParamTest()
        {
            var code = @"pub func hoge(a: i32, b: i64) {}".Trim();
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(func (export \"hoge\") (param $a i32) (param $b i64))" +
                         ")", actual);
        }

        [Fact]
        public void FunctionWithReturnTest()
        {
            var code = @"pub func hoge(): i32 {}".Trim();
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(func (export \"hoge\") (result i32))" +
                         ")", actual);
        }

        [Fact]
        public void ReadParameterTest()
        {
            var code = @"func hoge(a: i32): i32 { return a; }".Trim();
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module (func $hoge (param $a i32) (result i32) get_local $a return))", actual);
        }

        [Fact]
        public void AdditionTest()
        {
            var code = "func add(lhs: i32, rhs:i32): i32 {" +
                       "    return lhs + rhs;" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(func $add (param $lhs i32) (param $rhs i32) (result i32) " +
                                "get_local $lhs " +
                                "get_local $rhs " +
                                "i32.add " +
                                "return" +
                            ")" +
                         ")", actual);
        }
        [Fact]
        public void IntLiteralTest()
        {
            var code = "func getAnswer(): i32 {" +
                       "    return 42;" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(func $getAnswer (result i32) " +
                                "i32.const 42 " +
                                "return" +
                            ")" +
                         ")", actual);
        }
        [Fact]
        public void CallAnotherFunction()
        {
            var code = "func getAnswer(): i32 {" +
                       "    return 42;" +
                       "}" +
                       "pub func getAnswerPlus1(): i32 {" +
                       "    return getAnswer() + 1;" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(func $getAnswer (result i32) " +
                                "i32.const 42 " +
                                "return) " +
                            "(func (export \"getAnswerPlus1\") (result i32) " +
                                "call $getAnswer " +
                                "i32.const 1 " +
                                "i32.add " +
                                "return)" +
                         ")", actual);
        }
        [Fact]
        public void UseLocalVariable()
        {
            var code = "func hoge(): i32 {" +
                       "    var x = 10;" +
                       "    return x;" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(func $hoge (result i32) (local $x i32) " +
                                "i32.const 10 " +
                                "set_local $x " +
                                "get_local $x " +
                                "return)" +
                         ")", actual);
        }
        [Fact]
        public void UseLocalVariableSplit()
        {
            var code = "func hoge(): i32 {" +
                       "    var x: i32;" +
                       "    x = 10;" +
                       "    return x;" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(func $hoge (result i32) (local $x i32) " +
                                "i32.const 10 " +
                                "set_local $x " +
                                "get_local $x " +
                                "return)" +
                         ")", actual);
        }
        [Fact]
        public void IfExpressionWithoutResult()
        {
            var code = "func hoge(): i32 {" +
                       "    var x = 10;" +
                       "    if x == 10 {" +
                       "        return 1;" +
                       "    }" +
                       "    return 0;" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                             "(func $hoge (result i32) (local $x i32) " +
                                 "i32.const 10 " +
                                 "set_local $x " +
                                 "get_local $x " +
                                 "i32.const 10 " +
                                 "i32.eq " +
                                 "if " +
                                    "i32.const 1 " +
                                    "return " +
                                 "end " +
                                 "i32.const 0 " +
                                 "return)" +
                         ")", actual);
        }
        [Fact]
        public void IfExpressionWithResult()
        {
            var code = "func hoge(): i32 {" +
                       "    var x = 10;" +
                       "    var y = if x == 10 {" +
                       "        1;" +
                       "    } else {" +
                       "        0;" +
                       "    }" +
                       "    y;" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                             "(func $hoge (result i32) (local $x i32) (local $y i32) " +
                                 "i32.const 10 " +
                                 "set_local $x " +
                                 "get_local $x " +
                                 "i32.const 10 " +
                                 "i32.eq " +
                                 "if (result i32) " +
                                     "i32.const 1 " +
                                 "else " +
                                     "i32.const 0 " +
                                 "end " +
                                 "set_local $y " +
                                 "get_local $y)" +
                         ")", actual);
        }

        [Fact]
        public void Extern()
        {
            var code = "extern func console.log(x: i32);" +
                       "func hoge() {" +
                       "    console.log(10)" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(import \"console\" \"log\" (func $console.log (param $x i32))) " +
                            "(func $hoge " +
                                "i32.const 10 " +
                                "call $console.log)" +
                         ")", actual);
        }
        [Fact]
        public void StringWithVariable()
        {
            var code = /*"use memory 1 from js.mem;" + */
                       "extern func console.log(s: string);" +
                       "func hoge() {" +
                       "    var x = \"hoge\";" +
                       "    console.log(x)" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(import \"js\" \"mem\" (memory 1)) " +
                            "(data (i32.const 0) \"hoge\") " +
                            "(import \"console\" \"log\" (func $console.log (param $s_0 i32) (param $s_1 i32))) " +
                            "(func $hoge (local $x_0 i32) (local $x_1 i32) " +
                                "i32.const 0 " +
                                "set_local $x_0 " +
                                "i32.const 4 " +
                                "set_local $x_1 " +
                                "get_local $x_0 " +
                                "get_local $x_1 " +
                                "call $console.log)" +
                         ")", actual);
        }

        [Fact]
        public void StringWithoutVariable()
        {
            var code = /*"use memory 1 from js.mem;" + */
                "extern func console.log(s: string);" +
                "func hoge() {" +
                "    console.log(\"hoge\")" +
                "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                             "(import \"js\" \"mem\" (memory 1)) " +
                             "(data (i32.const 0) \"hoge\") " +
                             "(import \"console\" \"log\" (func $console.log (param $s_0 i32) (param $s_1 i32))) " +
                             "(func $hoge " +
                                "i32.const 0 " +
                                "i32.const 4 " +
                                "call $console.log)" +
                         ")", actual);
        }

        [Fact]
        public void Loop()
        {
            var code = "pub func hoge(): i32 {" +
                       "    var i = 0" +
                       "    loop {" +
                       "        i = i + 1" +
                       "        if i == 5 { break; }" +
                       "    }" +
                       "    i" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                         "(func (export \"hoge\") (result i32) (local $i i32) " +
                            "i32.const 0 " +
                            "set_local $i " +
                            "block " +
                                "loop " +
                                    "get_local $i " +
                                    "i32.const 1 " +
                                    "i32.add " +
                                    "set_local $i " +
                                    "get_local $i " +
                                    "i32.const 5 " +
                                    "i32.eq " +
                                    "if " +
                                        "br 2 " +
                                    "end " +
                                    "br 0 " +
                                "end " +
                            "end " +
                            "get_local $i)" +
                        ")", actual);

        }

        [Fact]
        public void Comparers()
        {
            var code = "func hoge() {" +
                       "    var x = 1 > 10" +
                       "    x = 1 < 10" +
                       "    x = 1 >= 10" +
                       "    x = 1 <= 10" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);
            Assert.Equal("(module " +
                            "(func $hoge (local $x i32) " +
                                // >
                                "i32.const 1 " +
                                "i32.const 10 " +
                                "i32.gt_s " +
                                "set_local $x " +
                                // <
                                "i32.const 1 " +
                                "i32.const 10 " +
                                "i32.lt_s " +
                                "set_local $x " +
                                // >=
                                "i32.const 1 " +
                                "i32.const 10 " +
                                "i32.ge_s " +
                                "set_local $x " +
                                // <=
                                "i32.const 1 " +
                                "i32.const 10 " +
                                "i32.le_s " +
                                "set_local $x" +
                         "))", actual);
        }

        [Fact]
        public void PlusEqual()
        {
            var code = "func hoge() {" +
                       "    var x = 1" +
                       "    x += 1" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);
            Assert.Equal("(module (func $hoge (local $x i32) " +
                            "i32.const 1 " +
                            "set_local $x " +
                            "i32.const 1 " +
                            "get_local $x " +
                            "i32.add " +
                            "set_local $x" +
                         "))", actual);
        }

        [Fact]
        public void NumberArray()
        {
            var code = "func hoge(): i32 {" +
                       "    var x = new i32[1]" +
                       "    x[0] = 1" +
                       "    x[0]" +
                       "}";
            var tree = new WasmicSyntaxTree().ParseText(code);
            var actual = WasmicCompiler.Compile(tree);

            Assert.Equal("(module " +
                            "(import \"js\" \"mem\" (memory 1)) " +
                            "(func $hoge (result i32) (local $x i32) " +
                                // x = 0 (offset in memory)
                                "i32.const 0 " +
                                "set_local $x " +
                                // get offset x[0]
                                "get_local $x " +
                                "i32.const 0 " +
                                "i32.add " +
                                // store 1 in x[0]
                                "i32.const 1 " +
                                "i32.store " +
                                // get x[0]
                                "get_local $x " +
                                "i32.const 0 " +
                                "i32.add " +
                                "i32.load" +
                         "))", actual);
        }
    }
}
