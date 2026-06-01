module Cli4096.Program

open System
open System.Text.RegularExpressions

// ---------------------------------------------------------------------------
// Domain types
// ---------------------------------------------------------------------------

/// A move direction. (Req 3)
type Direction =
    | Up
    | Down
    | Left
    | Right

/// The board is a 4x4 grid of ints. 0 represents an empty cell; any other
/// value is the numeric value of the tile in that cell. Rows are top-to-bottom,
/// columns left-to-right, both 0-indexed internally and shown as 1..4 to the user.
type Board = int list list

let boardSize = 4
let winningTile = 4096
let buffStep = 3000

// ---------------------------------------------------------------------------
// Console input helper
// ---------------------------------------------------------------------------

/// Read a line, treating EOF (null) as an empty string so the game never crashes.
let private readLine () =
    match Console.ReadLine() with
    | null -> ""
    | s -> s

// ---------------------------------------------------------------------------
// Board access helpers (0-indexed)
// ---------------------------------------------------------------------------

let private getCell (board: Board) row col = board |> List.item row |> List.item col

/// Return a new board with cell (row, col) set to value (immutable update).
let private setCell (board: Board) row col value : Board =
    board
    |> List.mapi (fun i r ->
        if i = row then r |> List.mapi (fun j v -> if j = col then value else v)
        else r)

/// Coordinates (0-indexed) of every empty cell.
let private emptyCells (board: Board) =
    [ for i in 0 .. boardSize - 1 do
          for j in 0 .. boardSize - 1 do
              if getCell board i j = 0 then yield (i, j) ]

let private emptyBoard : Board = List.replicate boardSize (List.replicate boardSize 0)

// ---------------------------------------------------------------------------
// Slide & merge logic (Req 4, Req 5)
// ---------------------------------------------------------------------------

/// Slide a single row to the left, merging equal adjacent tiles.
/// Each tile may take part in at most one merge per move, e.g.
/// [2;2;2;2] -> [4;4;0;0]. Returns the resulting row and the score gained
/// (sum of the values of all tiles produced by merges).
let private slideRowLeft (row: int list) : int list * int =
    let tiles = row |> List.filter (fun v -> v <> 0)

    let rec merge remaining acc gained =
        match remaining with
        | a :: b :: rest when a = b ->
            // Two equal tiles merge into one of double the value.
            let merged = a + b
            merge rest (merged :: acc) (gained + merged)
        | a :: rest -> merge rest (a :: acc) gained
        | [] -> (List.rev acc, gained)

    let merged, gained = merge tiles [] 0
    let padded = merged @ List.replicate (boardSize - List.length merged) 0
    (padded, gained)

let private slideBoardLeft (board: Board) : Board * int =
    let results = board |> List.map slideRowLeft
    (results |> List.map fst, results |> List.sumBy snd)

/// Apply a move in the given direction. Returns the new board and the score
/// gained by merges during this move. Implemented by reducing every direction
/// to "slide left" via row reversal and transposition.
let private applyMove (dir: Direction) (board: Board) : Board * int =
    match dir with
    | Left -> slideBoardLeft board
    | Right ->
        let b, g = board |> List.map List.rev |> slideBoardLeft
        (b |> List.map List.rev, g)
    | Up ->
        let b, g = board |> List.transpose |> slideBoardLeft
        (b |> List.transpose, g)
    | Down ->
        let b, g = board |> List.transpose |> List.map List.rev |> slideBoardLeft
        (b |> List.map List.rev |> List.transpose, g)

// ---------------------------------------------------------------------------
// Tile spawning (Req 2, Req 6)
// ---------------------------------------------------------------------------

let private rng = Random()

/// Place one new tile in a uniformly chosen empty cell. The tile is a 2 with
/// 50% probability or a 4 with 50% probability. If the board is full the board
/// is returned unchanged (callers only spawn after a move that frees a cell).
let private spawnTile (board: Board) : Board =
    match emptyCells board with
    | [] -> board
    | cells ->
        let (i, j) = cells.[rng.Next(cells.Length)]
        let value = if rng.Next(2) = 0 then 2 else 4
        setCell board i j value

// ---------------------------------------------------------------------------
// End conditions (Req 9, Req 10)
// ---------------------------------------------------------------------------

/// Win: any tile has reached the winning value.
let private hasWon (board: Board) =
    board |> List.exists (List.exists (fun v -> v = winningTile))

/// Loss: the board is full AND no two horizontally or vertically adjacent
/// tiles share the same value.
let private hasLost (board: Board) =
    let isFull = board |> List.forall (List.forall (fun v -> v <> 0))
    if not isFull then
        false
    else
        let anyAdjacentEqual (rows: Board) =
            rows
            |> List.exists (fun r -> r |> List.pairwise |> List.exists (fun (a, b) -> a = b))
        not (anyAdjacentEqual board || anyAdjacentEqual (List.transpose board))

// ---------------------------------------------------------------------------
// Rendering (Req 1)
// ---------------------------------------------------------------------------

/// Print the 4x4 board with row/column labels 1..4 and the current score.
/// Empty cells are shown as a single dash.
let private renderBoard (board: Board) (score: int) =
    let cellText v = if v = 0 then "-" else string v
    let colHeader =
        "    " + (String.concat "" [ for c in 1 .. boardSize -> sprintf "%6d" c ])
    printfn ""
    printfn "%s" colHeader
    board
    |> List.iteri (fun i row ->
        let cells = String.concat "" [ for v in row -> sprintf "%6s" (cellText v) ]
        printfn "%3d %s" (i + 1) cells)
    printfn ""
    printfn "Score: %d" score
    printfn ""

// ---------------------------------------------------------------------------
// Input parsing (Req 3, Req 8)
// ---------------------------------------------------------------------------

/// Parse a direction key. Case-sensitive: only lowercase w/a/s/d are accepted.
let private parseDirection (input: string) : Direction option =
    match input.Trim() with
    | "w" -> Some Up
    | "a" -> Some Left
    | "s" -> Some Down
    | "d" -> Some Right
    | _ -> None

/// Parse a "(row, column)" cell reference. Whitespace inside (and around) the
/// parentheses is ignored. Both numbers must be in the range 1..4. Returns
/// 0-indexed coordinates on success.
let private parseCell (input: string) : (int * int) option =
    let stripped = input |> String.filter (fun c -> not (Char.IsWhiteSpace c))
    let m = Regex.Match(stripped, @"^\((\d+),(\d+)\)$")
    if m.Success then
        let r = int m.Groups.[1].Value
        let c = int m.Groups.[2].Value
        if r >= 1 && r <= boardSize && c >= 1 && c <= boardSize then Some(r - 1, c - 1)
        else None
    else
        None

// ---------------------------------------------------------------------------
// Buff system (Req 7, Req 8)
// ---------------------------------------------------------------------------

/// Repeatedly prompt for a cell until a valid "(row, column)" is entered.
let rec private readCell (prompt: string) : int * int =
    printf "%s" prompt
    match parseCell (readLine ()) with
    | Some cell -> cell
    | None ->
        printfn "Invalid cell. Please enter as (row, column) with both values from 1 to 4."
        readCell prompt

/// Buff 1: remove a tile from a chosen cell. The cell must currently contain a
/// tile; otherwise (or on a malformed cell) the user is asked to retry.
let rec private applyRemoveTile (board: Board) : Board =
    let (r, c) = readCell "Enter target cell as (row, column): "
    if getCell board r c = 0 then
        printfn "That cell is empty. Please choose a cell that contains a tile."
        applyRemoveTile board
    else
        setCell board r c 0

/// Buff 2: swap the contents of a source and a destination cell. Every
/// combination (two tiles, one tile, both empty, same cell) is permitted and no
/// merge ever occurs.
let private applySwapTiles (board: Board) : Board =
    let (sr, sc) = readCell "Enter source cell as (row, column): "
    let (dr, dc) = readCell "Enter destination cell as (row, column): "
    let srcValue = getCell board sr sc
    let dstValue = getCell board dr dc
    board |> fun b -> setCell b sr sc dstValue |> fun b -> setCell b dr dc srcValue

/// Show one buff menu, get a valid choice (1 or 2), apply it and return the
/// updated board. Invalid menu input is rejected and re-prompted.
let rec private runBuffMenu (board: Board) : Board =
    printfn "Buff Selection! Enter 1 to remove a tile, or 2 to swap two tiles."
    match (readLine ()).Trim() with
    | "1" -> applyRemoveTile board
    | "2" -> applySwapTiles board
    | _ ->
        printfn "Please enter 1 or 2."
        runBuffMenu board

/// Run the buff menu once for each 3000-point threshold crossed by the latest
/// move, in order. The board is shown before each menu so the user can pick
/// cells; the score never changes while buffs are applied.
let rec private runBuffs (board: Board) (score: int) (remaining: int) : Board =
    if remaining <= 0 then
        board
    else
        renderBoard board score
        let updated = runBuffMenu board
        runBuffs updated score (remaining - 1)

// ---------------------------------------------------------------------------
// Game loop (Req 11 ordering)
// ---------------------------------------------------------------------------

let rec private gameLoop (board: Board) (score: int) =
    renderBoard board score
    printf "Enter your move (w=up, a=left, s=down, d=right): "
    let input = readLine ()

    match parseDirection input with
    | None ->
        // Invalid input: no tile spawned, prompt again. (Req 6)
        printfn "Invalid input. Please enter one of: w, a, s, d."
        gameLoop board score
    | Some dir ->
        let movedBoard, gained = applyMove dir board

        if movedBoard = board then
            // Move did not change the board: no spawn, no score change, retry. (Req 5, 6)
            printfn "That move does not change the board. Try a different direction."
            gameLoop board score
        else
            let scoreAfter = score + gained

            // (a) Spawn exactly one new tile. (Req 11a, Req 6)
            let spawned = spawnTile movedBoard

            // (b) Win check happens before any buff menu. (Req 11b, Req 9)
            if hasWon spawned then
                renderBoard spawned scoreAfter
                printfn "You win! A %d tile was created. Final score: %d" winningTile scoreAfter
            else
                // (c) Buff menu(s) for every 3000-point threshold crossed. (Req 11c, Req 7)
                let thresholdsCrossed = (scoreAfter / buffStep) - (score / buffStep)
                let afterBuffs = runBuffs spawned scoreAfter thresholdsCrossed

                // (d) Loss check. (Req 11d, Req 10)
                if hasLost afterBuffs then
                    renderBoard afterBuffs scoreAfter
                    printfn "Game over! No valid moves remain. Final score: %d" scoreAfter
                else
                    gameLoop afterBuffs scoreAfter

// ---------------------------------------------------------------------------
// Entry point (Req 2 initial state)
// ---------------------------------------------------------------------------

[<EntryPoint>]
let main _ =
    Console.OutputEncoding <- Text.Encoding.UTF8
    printfn "=== CLI 4096 with Buff System ==="
    printfn "Slide and merge tiles to reach %d. Reach a 3000-point threshold to earn a buff!" winningTile

    // Two starting tiles, each independently a 2 or a 4, in random empty cells.
    let startingBoard = emptyBoard |> spawnTile |> spawnTile
    gameLoop startingBoard 0
    0
