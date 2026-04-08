namespace DG.XrmTypeScript

open System
open Microsoft.Xrm.Sdk
open Microsoft.Xrm.Sdk.Metadata
open Microsoft.Xrm.Sdk.Client
open System.Runtime.Serialization

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

type OptionalNamespace = string option

type XdtGenerationSettings = {
  out: string option
  crmVersion: Version option
  skipForms: bool
  oneFile: bool
  useDeprecated: bool
  web: bool
  skipXrmApi: bool
  formIntersects: Intersect [] option
  labelMapping: (string * string)[] option
}

type EntityName = string

type XdtRetrievalSettings = {
  entities: EntityName[] option
  solutions: string[] option
  skipInactiveForms: bool
}

type ViewName = string
type AttributeName = string
type OwnedAttributes = AttributeName List
type Alias = string
type LinkedEntityName = string * Alias
type LinkedEntity = LinkedEntityName * AttributeName list
type LinkedAttributes = LinkedEntity list
type ParsedFetchXml = (EntityName * OwnedAttributes * LinkedAttributes)
type ViewData = (Guid * ViewName * ParsedFetchXml)

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
  nameMap: Map<EntityName, EntityInfo>

  [<field : DataMember>]
  bpfData: Entity[]
  
  [<field : DataMember>]
  formData: Map<string, Entity[]>
}
