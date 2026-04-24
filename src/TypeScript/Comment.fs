namespace DG.XrmTypeScript

open type System.String

[<AbstractClass; Sealed>]
type Comment =

  static member private Wrap lines =
    match lines with
    | [] -> []
    | [ line ] -> [ $"/** {line} */" ]
    | _ -> [ "/**" ] @ (lines |> List.collect (fun l -> [ ""; l ]) |> List.tail |> List.map (sprintf " * %s  ")) @ [ " */" ]

  static member Entity(displayName, setName, ?logicalName, ?isIntersect, ?intersectEntities) =
    let logicalName = defaultArg logicalName ""
    let intersectEntities = defaultArg intersectEntities []
    
    [ if not (IsNullOrWhiteSpace displayName) then yield $"**{displayName.Trim()}**"
      if not (IsNullOrWhiteSpace logicalName) then yield $"Logical Name: `{logicalName.Trim()}`"
      if not (IsNullOrWhiteSpace setName) then yield $"Set Name: `{setName.Trim()}`"
      if defaultArg isIntersect false then yield "Intersect Table"
      match intersectEntities with
      | (ln1, dn1) :: (ln2, dn2) :: _ -> yield $"Intersects: {dn1} (`{ln1}`) ⟷ {dn2} (`{ln2}`)"
      | _ -> () ]
    |> Comment.Wrap

  static member Attribute(displayName, ?colType, ?tes, ?link, ?label, ?isPrimaryId, ?logicalName) =
    let logicalName = defaultArg logicalName ""
    let tes = Option.defaultValue [||] tes
    let link = defaultArg link ""
    let label = defaultArg label ""

    [ if not (IsNullOrWhiteSpace displayName) then yield $"**{displayName.Trim()}**"
      if defaultArg isPrimaryId false then yield "Primary ID"
      match colType with Some t -> yield $"Column Type: {t}" | None -> ()
      if not (IsNullOrWhiteSpace logicalName) then yield $"Lookup Field: `{logicalName.Trim()}`"
      if tes.Length > 0 then
        let maxDisplay = 5
        let shown = tes |> Array.truncate maxDisplay |> Array.map (fun (ln, _, dn) -> $"{dn} (`{ln}`)") |> String.concat " | "
        let formatted = if tes.Length <= maxDisplay then shown else $"{shown} | +{tes.Length - maxDisplay} more"
        yield $"Table: {formatted}"
      if not (IsNullOrWhiteSpace label) then yield $"Label: {label.Trim()}"
      if not (IsNullOrWhiteSpace link) then yield sprintf "{@link %s}" (link.Trim()) ]
    |> Comment.Wrap

  static member Relationship(displayName: string, relType, partner: string, relatedEntity: string, ?intersectTable) =
    let intersectTable = defaultArg intersectTable ""
    [ yield $"Relationship Type: {relType}"
      if not (IsNullOrWhiteSpace relatedEntity) then yield $"Related entity: {displayName.Trim()} (`{relatedEntity.Trim()}`)"
      if not (IsNullOrWhiteSpace partner) then yield $"Partner navigation property: `{partner.Trim()}`"
      if not (IsNullOrWhiteSpace intersectTable) then yield $"Intersect table: `{intersectTable.Trim()}`" ]
    |> Comment.Wrap

  static member Basic(displayName, ?link) =
    let link = defaultArg link ""
    [ if not (IsNullOrWhiteSpace displayName) then yield $"**{displayName.Trim()}**"
      if not (IsNullOrWhiteSpace link) then yield sprintf "{@link %s}" (link.Trim()) ]
    |> Comment.Wrap

  static member Other(displayName, ?link, ?tab) =
    let link = defaultArg link ""
    let tab = defaultArg tab ""
    [ if not (IsNullOrWhiteSpace displayName) then yield $"**{displayName.Trim()}**"
      if not (IsNullOrWhiteSpace tab) then yield $"Tab: {tab.Trim()}"
      if not (IsNullOrWhiteSpace link) then yield sprintf "{@link %s}" (link.Trim()) ]
    |> Comment.Wrap
