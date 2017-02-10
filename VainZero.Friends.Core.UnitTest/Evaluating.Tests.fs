﻿namespace VainZero.Friends.Core

open Basis.Core
open Persimmon
open Persimmon.Syntax.UseTestNameByReflection

module ``test Term`` =
  let x = VarTerm (Variable.Create("X"))
  let y = VarTerm (Variable.Create("Y"))
  let listTerm = Term.listFromSeq

  let ``test seqFromList`` =
    let body ((headTerm, tailTerm), expected) =
      test {
        do! Term.seqFromList headTerm tailTerm |> assertEquals expected
      }
    parameterize {
      case
        ( (x, Term.nil)
        , ([x], Term.nil)
        )
      case
        ( (x, ConsTerm (y, Term.nil))
        , ([x; y], Term.nil)
        )
      case
        ( (x, ConsTerm (x, y))
        , ([x; x], y)
        )
      run body
    }

module ``test Environment`` =
  let x = VarTerm (Variable.Create("X"))
  let y = VarTerm (Variable.Create("Y"))
  let socrates = AtomTerm (Atom "socrates")
  let plato = AtomTerm (Atom "plato")
  let listTerm = Term.listFromSeq

  let ``test tryUnify success`` =
    let body (term, term', testTerm, expected) =
      test {
        match Environment.Empty |> Environment.tryUnify term term' with
        | Some env ->
          do! assertEquals (env.Substitute(term)) (env.Substitute(term'))
          do! env.Substitute(x) |> assertEquals expected
        | None ->
          return! fail "Unification failed."
      }
    parameterize {
      // Each atom matches the same atom.
      case
        ( socrates, socrates
        , x, x
        )
      // Variables match each other and don't refer to circularly.
      case 
        ( x, y
        , x, y
        )
      // Variables match any terms.
      case
        ( x, socrates
        , x, socrates
        )
      case
        ( x, listTerm [socrates; plato]
        , x, listTerm [socrates; plato]
        )
      // Each list term matches the list term with the same content.
      case
        ( listTerm [x; y]
        , listTerm [socrates; plato]
        , x, socrates
        )
      run body
    }

  let ``test tryUnify failure`` =
    let body (term, term') =
      test {
        match Environment.Empty |> Environment.tryUnify term term' with
        | Some env ->
          return! fail "Unification succeeded unexpectedly."
        | None ->
          return ()
      }
    parameterize {
      case (socrates, plato)
      case (listTerm [x], listTerm [x; y])
      run body
    }

module ``test Knowledge `` =
  let human = Predicate "human"
  let mortal = Predicate "mortal"
  let x = VarTerm (Variable.Create "X")
  let y = VarTerm (Variable.Create "Y")
  
  let socrates = AtomTerm (Atom "socrates")
  let plato = AtomTerm (Atom "plato")
  
  let socratesIsHuman =
    AxiomRule (Proposition.Create(human, socrates))
  let platoIsHuman =
    AxiomRule (Proposition.Create(human, plato))
  let humanIsMortal =
    InferRule (Proposition.Create(mortal, x), Proposition.Create(human, x))

  let socratesKnowledge =
    Knowledge.Empty
      .Add(socratesIsHuman)
      .Add(platoIsHuman)
      .Add(humanIsMortal)

  let ``test Add and FindAll`` =
    test {
      let knowledge = socratesKnowledge
      do!
        knowledge.FindAll(human)
        |> Seq.toArray
        |> assertEquals [|socratesIsHuman; platoIsHuman|]
      do!
        knowledge.FindAll(mortal)
        |> Seq.toArray
        |> assertEquals [|humanIsMortal|]
      do!
        knowledge.FindAll(Predicate "unknown-predicate")
        |> Seq.isEmpty
        |> assertEquals true
    }

  let ``test prove`` =
    test {
      let knowledge = socratesKnowledge
      do!
        knowledge
        |> Knowledge.prove (Proposition.Create(human, socrates)) Environment.Empty
        |> Seq.length
        |> assertEquals 1
      do!
        knowledge
        |> Knowledge.prove (Proposition.Create(mortal, x)) Environment.Empty
        |> Seq.length
        |> assertEquals 2
    }

  let ``test query`` =
    let body (prop, expected) =
      test {
        do!
          socratesKnowledge
          |> Knowledge.query (Proposition.Create(mortal, x))
          |> Seq.map 
            (fun assignments ->
              assignments |> Array.map (fun (var, term) -> (var.Name, term))
            )
          |> Seq.toArray
          |> assertEquals expected
      }
    parameterize {
      case
        ( Proposition.Create(mortal, x)
        , [|
            [|("X", socrates)|]
            [|("X", plato)|]
          |]
        )
      run body
    }
