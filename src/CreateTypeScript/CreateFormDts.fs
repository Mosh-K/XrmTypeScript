module internal DG.XrmTypeScript.CreateFormDts

open TsStringUtil
open IntermediateRepresentation
open Utility


let Attributes = {|
  Base = "Xrm.Attributes.Attribute"
  String = "Xrm.Attributes.StringAttribute"
  Number = "Xrm.Attributes.NumberAttribute"
  Boolean = "Xrm.Attributes.BooleanAttribute"
  DateTime = "Xrm.Attributes.DateAttribute"
  OptionSet = "Xrm.Attributes.OptionSetAttribute"
  MultiSelectOptionSet = "Xrm.Attributes.MultiSelectOptionSetAttribute"
  Lookup = "Xrm.Attributes.LookupAttribute"
|}
let Controls = {|
  Base = "Xrm.Controls.Control"
  Standard = "Xrm.Controls.StandardControl"
  Tab = "Xrm.Controls.Tab"
  Section = "Xrm.Controls.Section"
  String = "Xrm.Controls.StringControl"
  Boolean = "Xrm.Controls.BooleanControl"
  Number = "Xrm.Controls.NumberControl"
  Date = "Xrm.Controls.DateControl"
  OptionSet = "Xrm.Controls.OptionSetControl"
  MultiSelectOptionSet = "Xrm.Controls.MultiSelectOptionSetControl"
  Lookup = "Xrm.Controls.LookupControl"
  SubGrid = "Xrm.Controls.GridControl"
  QuickForm = "Xrm.Controls.QuickFormControl"
  IFrame = "Xrm.Controls.IframeControl"
  WebResource = "Xrm.Controls.FramedControl"
|}
let Collections = {|
  Attributes = "Xrm.AttributeCollection"
  Controls = "Xrm.ControlCollection"
  Tabs = "Xrm.TabCollection"
  Sections = "Xrm.SectionCollection"
  QuickForms = "Xrm.QuickFormCollection"
  MatchingDelegate = "Xrm.Collection.MatchingDelegate"
|}

let Interfaces = {|
  Attributes = "Attributes"
  Controls = "Controls"
  Tabs = "Tabs"
  QuickForms = "QuickForms"
|}

let TabSectionsNs = "TabSections"
let SectionControlsNs = "SectionControls"

let AttrMap = "AttributeMap"
let CtrlMap = "ControlMap"
let TabUnion  = "TabUnion"
let QuickFormUnion = "QuickFormUnion"

let unionWithNull t canBeNull = 
  if canBeNull
  then 
    TsType.Union [t;TsType.Null]
  else
    t

/// Translate internal attribute type to corresponding TypeScript interface.
let getAttributeInterface ty canBeNull = 
  let returnType = 
    match ty with
    | AttributeType.OptionSet TsType.Boolean        -> TsType.Custom Attributes.Boolean
    | AttributeType.OptionSet ty            -> TsType.SpecificGeneric (Attributes.OptionSet, [ ty ])
    | AttributeType.MultiSelectOptionSet ty -> TsType.SpecificGeneric (Attributes.MultiSelectOptionSet, [ ty ])
    | AttributeType.Default TsType.String           -> TsType.Custom Attributes.String
    | AttributeType.Default ty              -> TsType.Custom Attributes.Base
    | AttributeType.Lookup ty               -> TsType.Generic (Attributes.Lookup, ty)
    | x                              -> TsType.Custom $"Xrm.Attributes.{x}Attribute"
  unionWithNull returnType canBeNull
 
/// Gets the corresponding enum of the option set if possible
let getOptionSetType = function
  | Some (_, _, AttributeType.OptionSet ty, _) 
  | Some (_, _, AttributeType.MultiSelectOptionSet ty, _) -> ty
  | _ -> TsType.Number

/// Translate internal control type to corresponding TypeScript interface.
let getControlInterface cType (attr: XrmFormAttribute option) canBeNull =
  let returnType = 
    match attr, cType with
    | None, ControlType.Default       -> TsType.Custom Controls.Standard
    | Some (_, _, AttributeType.Default TsType.String, _), ControlType.Default
                                      -> TsType.Custom Controls.String
    | Some (_, __, at, _), ControlType.Default    -> TsType.Custom Controls.Base
    | aType, ControlType.OptionSet -> 
        match aType with
        | Some (_, _, AttributeType.OptionSet TsType.Boolean, _) ->  TsType.Custom Controls.Boolean
        | _ -> TsType.SpecificGeneric (Controls.OptionSet, [ getOptionSetType aType ])
    | aType, ControlType.MultiSelectOptionSet
                                      -> TsType.SpecificGeneric (Controls.MultiSelectOptionSet, [ getOptionSetType aType ])
    | Some (_, _, AttributeType.Lookup _, _), ControlType.Lookup tes
    | _, ControlType.Lookup tes       -> TsType.Generic (Controls.Lookup, tes)
    | _, ControlType.SubGrid tes      -> TsType.Custom Controls.SubGrid
    | _, ControlType.Number       -> TsType.Custom Controls.Number
    | _, ControlType.Date       -> TsType.Custom Controls.Date
    | _, ControlType.IFrame      -> TsType.Custom Controls.IFrame
    | _, ControlType.WebResource      -> TsType.Custom Controls.WebResource
    | _, x                            -> TsType.Custom $"Xrm.Controls.{x}Control"
  unionWithNull returnType canBeNull

/// Default collection functions which also use the "get" function name.
let getDefaultFuncs funcName returnType =
  [ Function.Create(funcName, [ Variable.Create("name", TsType.String) ], TsType.Undefined)
    Function.Create(funcName, returnType = TsType.Array(TsType.Custom returnType))
    Function.Create(
      funcName,
      [ Variable.Create("index", TsType.Number) ],
      TsType.Union [ TsType.Custom returnType; TsType.Null ]
    )
    Function.Create(
      funcName,
      [ Variable.Create("delegateFunction", TsType.Generic(Collections.MatchingDelegate, returnType)) ],
      TsType.Array(TsType.Custom returnType)
    ) ]

let getKeyofFunction (funcName: string) (map: string) =
    Function.Create(
        $"{funcName}<T extends keyof {map}>",
        [ Variable.Create("name", TsType.Custom "T") ],
        TsType.Custom $"{map}[T]"
    )

let getNameofFunction (unionName: string) (returnType: string) = 
    Function.Create(
        "get",
        [ Variable.Create("name", TsType.Custom unionName) ],
        TsType.Custom returnType
    )

/// Generate Xrm.Page.data.entity.attributes.get(<string>) functions.
let attributeCollection  =
  let keyofFunc = getKeyofFunction "get" AttrMap
  let defaultFuncs = getDefaultFuncs "get" Attributes.Base
  Interface.Create(Interfaces.Attributes, extends = [ Collections.Attributes ], funcs = keyofFunc :: defaultFuncs)

/// Generate Xrm.Page.data.entity.attributes Map.
let getAttributeCollectionMap (attributes: XrmFormAttribute list) =
  let getVars =
    attributes
    |> List.map (fun (name, _, ty, canBeNull) ->
      Variable.Create(name, getAttributeInterface ty canBeNull))

  Interface.Create(AttrMap, vars = getVars)

/// Auxiliary function that determines if a control is to be included based on it's name and the crmVersion
let includeControl (name: string) (formType: string option) crmVersion =
    (not (name.StartsWith("header_")) && not (name.StartsWith("footer_")))
      || (crmVersion .>= (6,0,0,0) && not(formType.IsSome && formType.Value.Equals("Quick")))

let getControlCollectionFuncs (controls: XrmFormControl list) (formType: string option) (crmVersion: Version) =
    controls
    |> List.map (fun (name, comment, attr, cType, isBpf, canBeNull) ->
      let paramType = getConstantType name
      let returnType = getControlInterface cType attr canBeNull         
      match includeControl name formType crmVersion with
      | false -> None
      | true ->
        Some (Function.Create("get", [Variable.Create("name", paramType)], returnType, ?comment = comment))
      )
    |> List.choose id

/// Generate Xrm.Page.ui.controls.get(<string>) functions.
let controlCollection =
  let keyofFunc = getKeyofFunction "get" CtrlMap
  let defaultFuncs = getDefaultFuncs "get" Controls.Base
  Interface.Create(Interfaces.Controls, extends = [ Collections.Controls ], funcs = keyofFunc :: defaultFuncs)

/// Generate Xrm.Page.ui.controls map.
let getControlCollectionMap (form: XrmForm) (crmVersion: Version) =
  let getVars = 
    form.controls
    |> List.map (fun (name, _, aType, cType, _, canBeNull) ->
      let returnType = getControlInterface cType aType canBeNull          
      match includeControl name form.formType crmVersion with
      | false -> None
      | true -> Some (Variable.Create($"\"{name}\"", returnType))
      )
    |> List.choose id
    
  Interface.Create(CtrlMap, vars = getVars)

let nsName xrmForm = 
  sprintf "Form.%s%s" 
    (xrmForm.entityName |> Utility.sanitizeString)
    (match xrmForm.formType with
    | Some ty -> $".{ty}"
    | None   -> "")

let getQuickViewFormCollection (quickViewForms: XrmFormQuickViewForm list) (formMap: Map<System.Guid, XrmForm>) =
  let getFuncs =
    quickViewForms
    |> List.map (fun (name, displayName, (_, formId)) ->
      let returnType, target =
        match formMap.TryGetValue formId with
        | true, form ->
          let a = $"{nsName form}.{form.name}"
          TsType.Custom a, a
        | false, _ -> TsType.Custom Controls.QuickForm, ""

      Function.Create(
        "get",
        [ Variable.Create("name", getConstantType name) ],
        returnType,
        Comment.Create(displayName, link = target)
      ))

  let nameofFuncs =
    match quickViewForms with
    | [] -> []
    | _ -> [ getNameofFunction QuickFormUnion Controls.QuickForm ]

  let defaultFuncs = getDefaultFuncs "get" Controls.QuickForm

  Interface.Create(
    Interfaces.QuickForms,
    extends = [ Collections.QuickForms ],
    funcs = getFuncs @ nameofFuncs @ defaultFuncs
  )

let getQuickFormNames (quickViewForms: XrmFormQuickViewForm list) =
  let getNames =
    quickViewForms
    |> List.map (fun (name, _, _) -> getConstantType name)
  QuickFormUnion, TsType.Union getNames
  
/// Generate Xrm.Page.ui.tabs.get(<string>) functions.
let getTabCollection (tabs: XrmFormTab list) =
  let getFuncs =
    tabs
    |> List.map (fun t ->
      let target = $"{TabSectionsNs}.{t.iname}"

      Function.Create(
        "get",
        [ Variable.Create("name", getConstantType t.name) ],
        TsType.Generic(Controls.Tab, target),
        Comment.Create(t.displayName, link = target)))

  let nameofFunc = getNameofFunction TabUnion Controls.Tab
  let defaultFuncs =  getDefaultFuncs  "get" Controls.Tab
  Interface.Create(Interfaces.Tabs, extends = [ Collections.Tabs ], funcs = getFuncs @ [ nameofFunc ] @ defaultFuncs)

let getTabNames (tabs: XrmFormTab list) =
  let getNames =
    tabs
    |> List.map (fun t -> getConstantType t.name)
  TabUnion, TsType.Union getNames

/// Generate Xrm.Page.ui.tabs.get(<string>).sections.get(<string>) functions.
let getTabSections (tabs: XrmFormTab list) =
  let getFuncs (sections: XrmFormSection list) = 
    sections
    |> List.map (fun s ->
      let target = $"{SectionControlsNs}.{s.iname}"

      Function.Create(
        "get",
        [ Variable.Create("name", getConstantType s.name) ],
        TsType.Generic(Controls.Section, target),
        Comment.Create(s.displayName, link = target)
      ))

  let defaultFuncs = getDefaultFuncs "get" Controls.Section

  tabs |> List.map (fun t ->
    Interface.Create(t.iname, Comment.Create t.displayName, extends = [ Collections.Sections ],
      funcs = getFuncs t.sections @ defaultFuncs))

/// Generate Xrm.Page.ui.tabs.sections.get(<someSection>).controls.get(<string>) functions.
let getSectionControls (sections: XrmFormSection list) (formType: string option) (crmVersion: Version) =
    sections
    |> List.map (fun s ->
        Interface.Create(
            s.iname,
            Comment.Create(s.displayName, tab = s.tabDescription, link = $"{TabSectionsNs}.{s.tabIname}"),
            extends = [ Collections.Controls ],
            funcs =
                getControlCollectionFuncs s.controls formType crmVersion
                @ getDefaultFuncs "get" Controls.Base
        ))

/// Generate Xrm.Page.getAttribute(<string>) functions.
let getAttributeFuncs form (crmVersion: Version) =
  let controlMap =
    form.controls
    |> List.choose(fun (id, _, attr, _, _, _) ->
      match includeControl id form.formType crmVersion with
      | false -> None
      | true ->
        match attr with
        | Some (aName, _, _, _) -> Some (aName, id)
        | None -> None)
    |> Map.ofList

  let attrFuncs = 
    form.attributes
    |> List.filter (fun (aName, _, _, _) -> not(form.formType = Some "Quick") || controlMap |> Map.containsKey aName)
    |> List.map (fun (name, comment, ty, canBeNull) ->
      let paramType = getConstantType name
      let returnType = getAttributeInterface ty canBeNull
      Function.Create("getAttribute", 
        [ Variable.Create("attributeName", paramType) ], returnType, ?comment = comment))
  let keyofFunc: Function = getKeyofFunction "getAttribute" $"{form.name}.{AttrMap}"
  attrFuncs @ [keyofFunc] @ getDefaultFuncs "getAttribute" Attributes.Base

/// Generate Xrm.Page.getControl(<string>) functions.
let getControlFuncs form (crmVersion: Version)=
  let ctrlFuncs = 
    form.controls
    |> List.map (fun (name, comment, aType, cType, isBpf, canBeNull) ->
      let paramType = getConstantType name
      let returnType = getControlInterface cType aType canBeNull
      match includeControl name form.formType crmVersion with
      | false -> None
      | true ->
        Some (Function.Create("getControl", 
               [ Variable.Create("controlName", paramType) ], returnType, ?comment = comment))
      )
    |> List.choose id
  let keyofFuncs = if not (form.formType = Some "Quick") then [getKeyofFunction "getControl" $"{form.name}.{CtrlMap}"] else []
  ctrlFuncs @ keyofFuncs @ getDefaultFuncs "getControl" Controls.Base

/// Generate internal namespace for keeping track all the collections.
let getFormNamespace (form: XrmForm) formMap crmVersion =
  Namespace.Create(
    form.name,
    interfaces =
      [ attributeCollection
        controlCollection
        getQuickViewFormCollection form.quickViewForms formMap
        getTabCollection form.tabs
        getAttributeCollectionMap form.attributes
        getControlCollectionMap form crmVersion ],
    typeDecs =
      match form.quickViewForms with
      | [] -> []
      | _ -> [ getQuickFormNames form.quickViewForms ]
      @ [ getTabNames form.tabs ],
    namespaces =
      [ Namespace.Create(TabSectionsNs, interfaces = getTabSections form.tabs)
        Namespace.Create(
          SectionControlsNs,
          interfaces =
            (form.tabs
             |> List.collect (fun t ->
               getSectionControls t.sections form.formType crmVersion))
        ) ]
  )

/// Generate the interface for the Xrm.Page of the form.
let getFormInterface (form: XrmForm) crmVersion =
  let superClass =
    if (form.formType = Some "Quick") then
      Controls.QuickForm
    else
      sprintf "Xrm.FormContext<%s,%s,%s,%s>" 
        $"{form.name}.{Interfaces.Attributes}"
        $"{form.name}.{Interfaces.Controls}"
        $"{form.name}.{Interfaces.Tabs}"
        $"{form.name}.{Interfaces.QuickForms}"

  Interface.Create(form.name, extends = [superClass], 
    funcs = 
      (if not(form.formType = Some "Quick") then getAttributeFuncs form crmVersion else []) 
      @ getControlFuncs form crmVersion)

/// Generate the namespace containing all the form interface and internal 
/// namespaces for collections.
let getFormDts (form: XrmForm) (formMap: Map<System.Guid, XrmForm>) crmVersion = 
  Namespace.Create(
    nsName form,
    declare = true,
    namespaces = (if not(form.formType = Some "Quick") then [ getFormNamespace form formMap crmVersion] else []),
    interfaces = [ getFormInterface form crmVersion]) 
  |> nsToString
