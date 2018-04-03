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
        i32.const 42
        return)
    (func (export "getAnswerPlus1") (result i32)
        call $getAnswer
        i32.const 1
        i32.add
        return)
)
```

## Currently Available Syntax

### Functions

- Definition
    - `[modfiers] func [name]([argName:argType (, [argName:argType])*]?) [block]`

- Example
    ```wasmic
    func foo(x: i32):i32 {
        return x;
    }
    ```

- Modifiers
    - `pub`
    ```wasmic
    pub func foo() {}
    ```
    - `pub` functions are visible from js

### Function Calls

- Example
    ```wasmic
    func foo(x: i32): i32 { return x + 1; }
    pub func bar(): i32 { return foo(42); } // returns 43
    ```

### Loops

#### Infinite Loop

- Example
    ```wasmic
    func foo() {
        var i = 0
        loop {
            i = i + 1
            if i == 5 { break }
        }
    }
    ```

### Operations

- Addition
    ```wasmic
    10 + 30
    ```
- ~~Subtraction~~
- ~~Multiplication~~
- ~~Division~~

### Types

- Native Types
    - `i32`
    - `i64`
    - ~~`f32`~~
    - ~~`f64`~~

### Imports

- Example
    - ws
    ```wasmic
    extern func console.log(x: i32);
    pub func foo() {
        console.log(10);
    }
    ```
    - js
    ```javascript
    const wasmInstance =
      new WebAssembly.Instance(wasmModule, {console: { log: function(x) { console.log(x)} } });
    const { foo } = wasmInstance.exports;
    foo()
    ```