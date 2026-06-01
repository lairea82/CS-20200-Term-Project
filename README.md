# CLI 4096 with Buff System

A terminal implementation of the 2048-style sliding-tile game. Slide and merge
numbered tiles to create a **4096** tile. As your score grows, a buff selection
system lets you remove or swap tiles every 3000 points.

The authoritative requirements for this game are in the submitted proposal
(requirements PDF). This README explains how to run it, records any changes from
the proposal, and documents the use of an LLM.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (the project targets `net10.0`).

Verify your install:

```bash
dotnet --version    # should report a 10.x SDK
```

## How to run

From the repository root (the folder containing `Cli4096.fsproj`):

```bash
dotnet run
```

That single command builds and starts the game. There are no external
dependencies and no data files to configure.

## How to play

- The board is a 4x4 grid. Rows and columns are labelled `1`-`4`; an empty cell
  is shown as `-`. The current score is printed below the board.
- Move with `w` (up), `a` (left), `s` (down), `d` (right), then press Enter.
  Input is case-sensitive: only the lowercase letters are accepted.
- All tiles slide as far as possible in the chosen direction. Two tiles with the
  same value that collide merge into one tile of double the value (each tile
  merges at most once per move).
- Each move that changes the board spawns one new tile (a 2 or a 4) in a random
  empty cell.
- Every time the score crosses a new multiple of 3000, a **Buff Selection** menu
  appears. Choose `1` to remove a tile, or `2` to swap two tiles. Cells are
  entered as `(row, column)` with both values from 1 to 4, e.g. `(2, 3)`.
- You win the moment any tile reaches 4096. You lose when the board is full and
  no two adjacent tiles share a value. Either ending prints the final score and
  the program exits.

## Changes from the proposal

The implementation follows every requirement in the proposal as written,
including the score rules, the order of end-of-move checks (spawn -> win check
-> buff menu(s) -> loss check), the one-merge-per-tile rule, and the buff retry
behavior.

* **Whitespace Tolerance for Inputs:**
  * **Original Requirement:** The proposal stated that "only the lowercase letters w, a, s, d are accepted" for movement, and "Whitespace inside the parentheses is ignored" for cell inputs.
  * **Change:** The implementation now gracefully ignores surrounding whitespace for movement inputs (e.g., ` w ` is accepted as `w`) and strips all whitespace globally for cell inputs (e.g., ` ( 2 , 3 ) ` is accepted).
  * **Justification:** During playtesting in the terminal, it became evident that players frequently and accidentally add leading or trailing spaces before hitting Enter. Strictly rejecting these inputs resulted in a frustrating user experience. Relaxing the whitespace rules significantly improves playability while maintaining the core logic.


## Use of Large Language Models

An LLM was used as an interactive programming assistant during the development of this project. As required by the project specification, here is a detailed description of that experience.

- **What the LLM was used for:** I primarily used the LLM to help translate the core game mechanics into idiomatic F#. It assisted in structuring the purely functional, immutable board model (`int list list`) and drafting the foundational recursive logic for the slide/merge mechanics. It also helped outline the score tracking, tile spawning probabilities, win/loss state detection, and the initial skeleton for the command-line input parsing.

- **What had to be manually changed or reprompted:** The initial LLM outputs missed several subtle nuances required by the specification, which I had to manually correct:
  - **Buff Flow & UI Ordering:** The LLM initially failed to display the updated board *before* prompting the user for buff coordinates. I manually adjusted the game loop to ensure the board state is rendered first so the player can make an informed choice without the score changing prematurely.
  - **Multi-Threshold Logic:** The LLM's initial approach to tracking the 3000-point buff triggers was flawed and didn't logically account for a single move crossing multiple thresholds at once. I manually implemented the `(scoreAfter / buffStep) - (score / buffStep)` formula to queue the correct number of consecutive buff menus.
  - **Input Parsing (UX):** The LLM's generated code strictly rejected inputs with accidental whitespace. I manually modified the parsing logic using `String.filter` to gracefully handle trailing spaces and inner-parentheses spaces for better playability.
  - **End-of-Move Sequence:** I had to explicitly enforce the strict execution order (Spawn -> Win Check -> Buffs -> Loss Check) because the LLM initially evaluated the loss condition before triggering the buff menus.

- **The main point the LLM could not do correctly:** There were two major limitations. First, architecturally, the LLM struggled to implement the strict "one merge per tile per move" rule (e.g., ensuring `[2; 2; 2; 2]` becomes `[4; 4; 0; 0]` instead of `[8; 0; 0; 0]`) using purely immutable F# pattern matching. It frequently suggested using imperative loops. I had to manually design the recursive `merge` logic in `slideRowLeft` to strictly adhere to functional programming principles. Second, environmentally, the LLM could not verify the code on the required .NET 10 SDK. I had to manually compile, debug, and verify all terminal rendering behaviors (such as UTF-8 encoding alignment) natively in VS Code on macOS to ensure the game ran flawlessly.

