# Wasmic

## Sample Code

- Wasmic

```wasmic
func getAnswer(): i32 {
    return 42;
}
pub func getAnswerPlus1(): i32 {
    return getAnswer() + 1;
}
```

- WebAssembly Text Format

```
(module
    (func $getAnswer (result i32)
        i32.const 42)
    (func (export "getAnswerPlus1") (result i32)
        call $getAnswer
        i32.const 1
        i32.add)
)
```