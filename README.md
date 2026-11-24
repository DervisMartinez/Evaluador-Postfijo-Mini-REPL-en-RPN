# Mini-REPL en Notación Polaca Inversa (RPN) — C# 12 / .NET 8

Proyecto práctico para la asignatura **CAO604: Lenguajes de Programación** (Universidad de Carabobo, FACYT, Semestre 1-2026).

El proyecto implementa un evaluador interactivo de consola (**REPL**) en **Notación Polaca Inversa estricta (RPN / Postfija)** escrito en **C# 12 sobre .NET 8 LTS**, diseñado específicamente para materializar y demostrar las decisiones de diseño del lenguaje anfitrión: *binding*, *scoping* y *shadowing*, sistema de tipos algebraicos (`abstract record` + `readonly record struct`), aritmética `checked`, flujo de errores con `EvalResult<T>`, paso de parámetros (`by-value`, `in`, `ref`, `out`), memoria `Stack`/`Heap` con `Span<char>` (`ref struct`), y abstracción orientada a objetos mediante interfaces y composición.

---

## Requisitos del entorno

- **.NET SDK 8.0** o superior (compatible con .NET 8 LTS, .NET 9 y .NET 10 mediante `<RollForward>Major</RollForward>`).
- Compilación estricta activada (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` y `<Nullable>enable</Nullable>`).

---

## Cómo compilar, probar y ejecutar

Desde la raíz del repositorio:

```bash
# 1. Restaurar dependencias (xUnit para pruebas) y compilar toda la solución
dotnet restore
dotnet build MiniRepl.Rpn.sln

# 2. Ejecutar la suite completa de pruebas unitarias (61 casos)
dotnet test tests/RpnRepl.Tests/RpnRepl.Tests.csproj

# 3. Iniciar el Mini-REPL interactivo
dotnet run --project src/RpnRepl/RpnRepl.csproj

# Opcional: iniciar con traza paso a paso activada desde el arranque
dotnet run --project src/RpnRepl/RpnRepl.csproj -- --trace
```

---

## Arquitectura del proyecto

```text
Mini-REPL en RPN/
├── MiniRepl.Rpn.sln
├── README.md
├── Reporte_Tecnico_CSharp_RPN.pdf
├── src/RpnRepl/
│   ├── Program.cs                        # Bucle REPL, comandos interactivos y formateo de salida/traza
│   ├── Diagnostics/
│   │   ├── SourceSpan.cs                 # readonly record struct (posición léxica en el stack)
│   │   ├── RpnError.cs                   # ADT cerrado (abstract record) con todos los errores controlados
│   │   └── EvalResult.cs                 # Result<T, RpnError> como readonly record struct + [NotNullWhen]
│   ├── Types/
│   │   ├── RpnOperator.cs                # Enum de operadores binarios y metadatos
│   │   ├── RpnValue.cs                   # ADT cerrado: IntValue (long), FloatValue (double), BoolValue (bool)
│   │   ├── StackSlot.cs                  # Semántica L-value vs R-value (ValueSlot | SymbolSlot)
│   │   └── Token.cs                      # ADT cerrado de tokens producidos por el lexer
│   ├── Lexing/
│   │   └── SpanLexer.cs                  # Escáner ref struct sobre ReadOnlySpan<char> (cero asignaciones al segmentar)
│   ├── Memory/
│   │   ├── EvaluationStack.cs            # Pila de evaluación encapsulada + snapshots inmutables
│   │   ├── ScopeFrame.cs                 # Marco de alcance enlazado al padre para bloques locales
│   │   └── SymbolEnvironment.cs          # Tabla de símbolos jerárquica (IScopeLookup + ref ScopeFrame)
│   └── Engine/
│       ├── IRpnOperation.cs              # Interfaz polimórfica IRpnOperation + OperationRegistry por composición
│       ├── TypeChecker.cs                # Promoción numérica controlada (Int -> Float) y rechazo de Bool
│       ├── EvalOptions.cs                # Configuración (readonly record struct) y modelo TraceStep
│       └── RpnEvaluator.cs               # Evaluador sin estado con paso explícito de pila, entorno y ref/in/out
└── tests/RpnRepl.Tests/
    ├── LexerTests.cs
    ├── ArithmeticAndTypeTests.cs
    ├── ErrorHandlingTests.cs
    ├── VariablesAndParameterPassingTests.cs
    └── ScopingAndShadowingTests.cs
```

---

## Referencia rápida del lenguaje RPN

| Categoría | Elementos | Notas semánticas |
| :--- | :--- | :--- |
| **Literales** | `10`, `-5`, `3.5`, `1e-2`, `true`, `false` | Se parsean con `CultureInfo.InvariantCulture`. |
| **Aritmética** | `+`, `-`, `*`, `/`, `%` | `Int` con `Int` produce `Int` (división entera, aritmética `checked`); mezcla con `Float` promueve a `Float`. División por cero controlada. |
| **Comparación** | `==`, `!=`, `<`, `>`, `<=`, `>=` | Devuelven `Bool`. `==`/`!=` admiten pares numéricos o pares `Bool`. `<, >, <=, >=` exigen operandos numéricos. |
| **Variables** | `x 10 =`, `x 5 2 + =`, `x x 1 + =` | Asignación postfija (`L-value R-value =`). La tabla de símbolos se vincula al marco de alcance actual. |
| **Alcance local** | `begin`, `end` | Abren y cierran un `ScopeFrame` temporal. Las variables creadas o reasignadas dentro hacen *shadowing* y no sobrescriben el entorno global tras `end`. |
| **Comandos REPL** | `vars`, `stack`, `clear`, `reset`, `trace`, `help`, `exit` | `vars` lista variables visibles e indica si están sombreando un alcance externo; `trace` alterna la traza token por token. |

---

## Sesión de prueba de ejemplo (entradas en RPN y salidas esperadas)

> En Notación Polaca Inversa (RPN), los operandos se apilan de izquierda a derecha y cada operador binario extrae primero el operando derecho (tope) y luego el izquierdo (`10 2 -` evalúa `10 - 2 = 8`), eliminando ambigüedades y paréntesis.

```text
rpn[global]> 5 1 2 + 4 * + 3 -
  => 14 : Int
rpn[global]> clear
Pila vaciada.
rpn[global]> 15 7 1 1 + - / 3 * 2 1 1 + + -
  => 5 : Int
rpn[global]> clear
Pila vaciada.
rpn[global]> x 10 =
  x = 10 : Int [nivel 0]
rpn[global]> x 5 + 2 *
  => 30 : Int
rpn[global]> clear
Pila vaciada.
rpn[global]> 5 2.5 +
  => 7.5 : Float
rpn[global]> 5 true +
  [!] Error de tipos en [7..8): '+' no admite operandos (Int, Bool); se esperaba (Int|Float, Int|Float).
    5 true +
           ^
rpn[global]> 10 0 /
  [!] Error aritmético en [5..6): división por cero en operación '/'.
    10 0 /
         ^
rpn[global]> begin
  → Entró a bloque local (nivel 1)
rpn[local:1]> x 99 =
  x = 99 : Int [nivel 1]
rpn[local:1]> y 3.5 =
  y = 3.5 : Float [nivel 1]
rpn[local:1]> vars
Entorno activo [bloque local (nivel 1)]:
  x = 99 : Int  (@ nivel 1)  [sombrea a x = 10 : Int @ nivel 0]
  y = 3.5 : Float  (@ nivel 1)
rpn[local:1]> x y +
  => 102.5 : Float  [pila: 2 elementos]
rpn[local:1]> end
  ← Salió de bloque local (global)
rpn[global]> vars
Entorno activo [global (nivel 0)]:
  x = 10 : Int  (@ nivel 0)
rpn[global]> x
  => 10 : Int  [pila: 3 elementos]
rpn[global]> clear
Pila vaciada.
rpn[global]> trace
Modo traza paso a paso: ACTIVADO.
rpn[global]> 3 4 2 * +
  [Traza de ejecución]
    #1  token='3     ' | nivel=0 | push 3 : Int                       | pila=[3 : Int]
    #2  token='4     ' | nivel=0 | push 4 : Int                       | pila=[3 : Int, 4 : Int]
    #3  token='2     ' | nivel=0 | push 2 : Int                       | pila=[3 : Int, 4 : Int, 2 : Int]
    #4  token='*     ' | nivel=0 | op '*' -> 8 : Int                  | pila=[3 : Int, 8 : Int]
    #5  token='+     ' | nivel=0 | op '+' -> 11 : Int                 | pila=[11 : Int]
  => 11 : Int
rpn[global]> exit
```

---

## Verificación automática

La suite de pruebas en `tests/RpnRepl.Tests` valida **61 casos** cubriendo:
- Tokenización sobre `ReadOnlySpan<char>`, manejo de BOM UTF-8, desplazamientos `SourceSpan` y errores léxicos/desbordamientos de literales.
- Aritmética entera `checked`, promoción `Int -> Float`, comparaciones lógicas y rechazo estricto de operaciones inválidas (`5 true +`, `5 true ==`, `true false <`).
- División por cero (`Int` y `Float`), *stack underflow*, asignación a literales (`5 10 =`), variables indefinidas y *rollback* transaccional ante errores a mitad de línea.
- Declaración, actualización autorreferenciada, copia por valor (`EvalOptions`) vs mutación por referencia (`EvaluationStack`, `ref`, `in`, `out`).
- Bloques anidados `begin ... end`, *shadowing* local y preservación intacta del entorno global tras `end`.
