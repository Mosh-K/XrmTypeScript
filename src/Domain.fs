namespace DG.XrmTypeScript

open System
open Microsoft.Xrm.Sdk
open Microsoft.Xrm.Sdk.Metadata
open Microsoft.Xrm.Sdk.Client
open System.Runtime.Serialization

type RelType =
  | ManyToOne
  | OneToMany
  | ManyToMany

type XrmAttributeType =
  | Boolean
  | Customer
  | DateTime
  | Decimal
  | Double
  | Integer
  | Lookup
  | Memo
  | Money
  | Owner
  | PartyList
  | Picklist
  | State
  | Status
  | String
  | Uniqueidentifier
  | CalendarRules
  | Virtual
  | BigInt
  | ManagedProperty
  | EntityName
  | Image
  | MultiSelectPicklist
  | File

module XrmAttributeType =
  let private map = [
    AttributeTypeDisplayName.BooleanType,             Boolean
    AttributeTypeDisplayName.CustomerType,            Customer
    AttributeTypeDisplayName.DateTimeType,            DateTime
    AttributeTypeDisplayName.DecimalType,             Decimal
    AttributeTypeDisplayName.DoubleType,              Double
    AttributeTypeDisplayName.IntegerType,             Integer
    AttributeTypeDisplayName.LookupType,              Lookup
    AttributeTypeDisplayName.MemoType,                Memo
    AttributeTypeDisplayName.MoneyType,               Money
    AttributeTypeDisplayName.OwnerType,               Owner
    AttributeTypeDisplayName.PartyListType,           PartyList
    AttributeTypeDisplayName.PicklistType,            Picklist
    AttributeTypeDisplayName.StateType,               State
    AttributeTypeDisplayName.StatusType,              Status
    AttributeTypeDisplayName.StringType,              String
    AttributeTypeDisplayName.UniqueidentifierType,    Uniqueidentifier
    AttributeTypeDisplayName.CalendarRulesType,       CalendarRules
    AttributeTypeDisplayName.VirtualType,             Virtual
    AttributeTypeDisplayName.BigIntType,              BigInt
    AttributeTypeDisplayName.ManagedPropertyType,     ManagedProperty
    AttributeTypeDisplayName.EntityNameType,          EntityName
    AttributeTypeDisplayName.ImageType,               Image
    AttributeTypeDisplayName.MultiSelectPicklistType, MultiSelectPicklist
    AttributeTypeDisplayName.FileType,                File ]

  let fromDisplayName t =
    map |> List.find (fst >> (=) t) |> snd

type Version = int * int * int * int
type Intersect = string * Guid[]

type ConnectionType = 
  | Proxy
  | OAuth
  | ClientSecret
  | ConnectionString

type XrmAuthSettings = {
  url: Uri
  method: ConnectionType option
  username: string option
  password: string option
  domain: string option
  ap: AuthenticationProviderType option
  clientId: string option
  returnUrl: string option
  clientSecret: string option
  connectionString: string option
}

type XdtGenerationSettings = {
  out: string
  crmVersion: Version option
  skipForms: bool
  oneFile: bool
  useDeprecated: bool
  web: bool
  skipXrmApi: bool
  formIntersects: Intersect []
  labelMapping: (string * string)[]
}

type XdtRetrievalSettings = {
  entities: string[] option
  solutions: string[] option
  skipInactiveForms: bool
}

[<DataContract>]
type EntityInfo = {
  [<field : DataMember(Name = "SchemaName")>]
  SchemaName: string

  [<field : DataMember(Name = "EntitySetName")>]
  EntitySetName: string

  [<field : DataMember(Name = "DisplayName")>]
  DisplayName: string
}

/// Serializable record containing necessary (meta)data
[<DataContract>]
type RawState = {

  [<field : DataMember>]
  crmVersion: Version

  [<field : DataMember>]
  metadata: EntityMetadata[]
  
  [<field : DataMember>]
  nameMap: Map<string, EntityInfo>

  [<field : DataMember>]
  bpfData: Entity[]
  
  [<field : DataMember>]
  formData: Map<string, Entity[]>
}
