namespace DG.XrmTypeScript

open System

type Value = 
  | String of string
  | Boolean of bool
  | NumberI of int
  | NumberD of double
  | List of Value list
  | Date of DateTime
  | Map of Map<string, Value>
  | Object of Map<string, Value>

type TsType = 
  | Void
  | Null
  | Undefined
  | Never
  | Any
  | Boolean
  | String
  | Number
  | Date
  | Array of TsType
  | Generic of string * string
  | SpecificGeneric of string * TsType list
  | Function of Variable list * TsType
  | Custom of string
  | EnumRef of string
  | Union of TsType list
  | Intersection of TsType list

and Variable = 
  { name : string
    varType : TsType option
    Comment: Comment option
    value : Value option
    declare: bool
    optional: bool }
  static member Create(name, ?varType, ?comment, ?value, ?declare, ?optional) = 
    { Variable.name = name
      varType = varType
      Comment = comment 
      value = value
      declare = defaultArg declare false
      optional = defaultArg optional false }

and Comment =
  { displayName: string
    label: string
    colType: XrmAttributeType option
    targetEntitySets: (string * string * string)[] option
    relType: RelType option
    tab: string
    link: string }
  static member Create(?displayName, ?label, ?colType, ?tes, ?relType, ?tab, ?link) =
    { displayName = defaultArg displayName ""
      label = defaultArg label ""
      colType = colType
      targetEntitySets = tes
      relType = relType
      tab = defaultArg tab ""
      link = defaultArg link "" }
  member c.ToCommentStrings() =
    let dsLine = if String.IsNullOrWhiteSpace c.displayName then None else Some $"**{c.displayName.Trim()}**"
    let tabLine = if String.IsNullOrWhiteSpace c.tab then None else Some $"Tab: {c.tab.Trim()}"
    let labelLine = if String.IsNullOrWhiteSpace c.label then None else Some $"Label: {c.label.Trim()}"
    let colTypeLine = c.colType |> Option.map (fun t -> $"Column Type: {t}")
    let tableLine =
      match c.targetEntitySets with
      | None | Some [||] -> None
      | Some tes ->
        let maxDisplay = 5
        let shown = tes |> Array.truncate maxDisplay |> Array.map (fun (ln, _, dn) -> $"{dn} (`{ln}`)") |> String.concat " | "
        let formatted = if tes.Length <= maxDisplay then shown else $"{shown} | +{tes.Length - maxDisplay} more"
        Some $"Table: {formatted}"
    let relTypeLine = c.relType |> Option.map (fun t -> $"Relationship Type: {t}")
    let linkLine = if String.IsNullOrWhiteSpace c.link then None else Some (sprintf "{@link %s}" (c.link.Trim()))

    let lines = [ dsLine; tabLine; labelLine; colTypeLine; tableLine; relTypeLine; linkLine ] |> List.choose id
    match lines with
    | [] -> []
    | [ line ] -> [ $"/** {line} */" ]
    | _ -> [ "/**" ] @ (lines |> List.map (sprintf " * %s  ")) @ [ " */" ]

type ExportType = 
  | Regular
  | Export

type TsEnum = 
  { name : string
    vals : (string * int option) list
    comment: Comment option
    declare : bool
    constant: bool
    export : bool }
  static member Create(name, ?vals, ?comment, ?constant, ?declare, ?export) = 
    { TsEnum.name = name
      vals = defaultArg vals []
      comment = comment
      declare = defaultArg declare false
      constant = defaultArg constant true
      export = defaultArg export false }

type Function = 
  { name : string
    comment : Comment option
    args : Variable list
    returnType : TsType option
    expr : string list }
  static member Create(name, ?args, ?returnType, ?comment, ?expr) = 
    { Function.name = name
      comment = comment
      args = defaultArg args []
      returnType = returnType
      expr = defaultArg expr [] }

type Class = 
  { name : string
    export : ExportType
    superClass : string option
    impls : string list
    consts : Variable list
    vars : Variable list
    funcs : Function list }
  static member Create(name, ?export, ?superClass, ?impls, ?consts, ?vars, 
                       ?funcs) = 
    { Class.name = name
      export = defaultArg export Regular
      superClass = superClass
      impls = defaultArg impls []
      consts = defaultArg consts []
      vars = defaultArg vars []
      funcs = defaultArg funcs [] }

type Interface = 
  { name : string
    comment : Comment option
    extends : string list
    export : ExportType
    vars : Variable list
    funcs : Function list }
  static member Create(name, ?comment, ?extends, ?export, ?vars, ?funcs) = 
    { Interface.name = name
      comment = comment
      extends = defaultArg extends []
      export = defaultArg export Regular
      vars = defaultArg vars []
      funcs = defaultArg funcs [] }

type Namespace = 
  { name : string
    export : ExportType
    declare : bool
    ambient : bool
    vars : Variable list
    enums : TsEnum list
    funcs : Function list
    namespaces : Namespace list
    interfaces : Interface list
    classes : Class list
    typeDecs : (string * TsType) list
  }
  static member Create(name, ?export, ?declare, ?ambient, ?vars, ?enums, 
                       ?namespaces, ?funcs, ?interfaces, ?classes, ?typeDecs) = 
    { Namespace.name = name
      export = defaultArg export Regular
      declare = defaultArg declare false
      ambient = defaultArg ambient false || defaultArg declare false
      vars = defaultArg vars []
      enums = defaultArg enums []
      funcs = defaultArg funcs []
      namespaces = defaultArg namespaces []
      interfaces = defaultArg interfaces []
      classes = defaultArg classes []
      typeDecs = defaultArg typeDecs [] 
    }


type TsType with
  static member fromEnum (e : TsEnum) = TsType.Custom e.name
  static member fromInterface (i : Interface) = TsType.Custom i.name