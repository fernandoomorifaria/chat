module Json

open System
open System.Text.Json
open System.Text.Json.Serialization
open Types

type MessageTypeConverter() =
    inherit JsonConverter<MessageType>()
    
    override _.Read(reader: byref<Utf8JsonReader>, typeToConvert: Type, options: JsonSerializerOptions) =
        match reader.GetString() with
        | "Text" -> Text
        | "System" -> System
        | _ -> failwith "Invalid MessageType"
    
    override _.Write(writer: Utf8JsonWriter, value: MessageType, options: JsonSerializerOptions) =
        match value with
        | Text -> writer.WriteStringValue "text"
        | System -> writer.WriteStringValue "system"

let private options: JsonSerializerOptions = 
    let options = JsonSerializerOptions()
    options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    options.Converters.Add(MessageTypeConverter())
    
    options

let serialize obj = JsonSerializer.Serialize(obj, options)
let deserialize<'T> (json: string) = JsonSerializer.Deserialize<'T>(json, options)