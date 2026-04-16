module DG.XrmTypeScript.IntermediateRepresentation

open Microsoft.Xrm.Sdk.Metadata

type Option = {
  label: string 
  value: int
}

type OptionSet = {
  name: string
  options: Option[]
  displayName: string
}

type SpecialType = 
  | Default 
  | OptionSet 
  | MultiSelectOptionSet
  | Money 
  | Guid 
  | EntityReference
  | Decimal

type XrmAttribute = { 
  schemaName: string
  logicalName: string
  varType: TsType
  specialType: SpecialType
  targetEntitySets: (string * string * string)[] option
  colType: XrmAttributeType
  readable: bool
  createable: bool
  updateable: bool
  displayName: string
}

type XrmOneToManyRelationship = {
  relatedInfo: EntityInfo list
  rawRelationship: OneToManyRelationshipMetadata
}

type XrmManyToManyRelationship = {
  relatedInfo: EntityInfo list
  rawRelationship: ManyToManyRelationshipMetadata
}

type XrmEntity = {
  schemaName: string
  logicalName: string
  idAttribute: string
  attributes: XrmAttribute list 
  optionSets: OptionSet list
  oneToManyRelationships: XrmOneToManyRelationship list
  manyToOneRelationships: XrmOneToManyRelationship list
  manyToManyRelationships: XrmManyToManyRelationship list
  displayName: string
}

// Forms
type ControlType = 
  | Default
  | Number
  | Date
  | Lookup of string
  | OptionSet
  | MultiSelectOptionSet
  | SubGrid
  | WebResource
  | IFrame
  | KBSearch

type AttributeType = 
  | Default of TsType
  | Number
  | Lookup of string
  | Date
  | OptionSet of TsType
  | MultiSelectOptionSet of TsType

type FormType =
  | Dashboard = 0
  | AppointmentBook = 1
  | Main = 2
  | MiniCampaignBO = 3
  | Preview = 4
  | Mobile = 5
  | Quick = 6
  | QuickCreate = 7
  | Dialog = 8
  | TaskFlowForm = 9
  | InteractionCentricDashboard = 10
  | Card = 11
  | MainInteractionCentric = 12
  | Other = 100
  | MainBackup = 101
  | AppointmentBookBackup = 102

type CanBeNull = bool
type QuickViewReference = string * System.Guid

type XrmFormAttribute = string * Comment option * AttributeType * CanBeNull
type XrmFormControl = string * Comment option * XrmFormAttribute option * ControlType * bool * CanBeNull
type XrmFormTab =
  { iname: string
    name: string
    displayName: string
    sections: XrmFormSection list }
and XrmFormSection =
  { iname: string
    name: string
    displayName: string
    controls: XrmFormControl list
    tabIname: string
    tabDescription: string }
type XrmFormQuickViewForm = string * string * QuickViewReference
  
type ControlClassId =
  | CheckBox | DateTime | Decimal | Duration | EmailAddress | EmailBody 
  | Float | IFrame | Integer | Language | Lookup | MoneyValue | Notes
  | PartyListLookup | Picklist | RadioButtons | RegardingLookup | MultiPicklist
  | StatusReason | TextArea | TextBox | TickerSymbol | TimeZonePicklist | Url
  | WebResource | Map | Subgrid | QuickView | Timer | KnowledgeBaseSearch
  | Other
  with override x.ToString() = x.GetType().Name

type ControlField = { 
  id: string
  dataFieldName: string
  displayName: string
  controlClass: ControlClassId
  canBeNull: CanBeNull
  isBpf: bool
  targetEntitySets: string option
  quickViewForms: string list option 
}


type XrmForm = {
  entityName: string
  entityDependencies: string seq
  guid: System.Guid option
  formType: string option
  name: string
  attributes: XrmFormAttribute list
  controls: XrmFormControl list
  quickViewForms: XrmFormQuickViewForm list
  tabs: XrmFormTab list
}

type InterpretedState = {
  outputDir: string
  entities: XrmEntity[]
  rawEntities: EntityMetadata[]
  forms: XrmForm[]
  bpfControls: Map<string,ControlField list>
}
