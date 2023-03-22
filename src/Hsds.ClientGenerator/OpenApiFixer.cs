using System.Text;

namespace Hsds.ClientGenerator;

public class OpenApiFixer
{
    public static string Apply(string openApiJsonString)
    {
        // downgrade version
        openApiJsonString = openApiJsonString.Replace("3.1.0", "3.0.3");

        // 
        var lines = GetLines(openApiJsonString);

        return string.Join('\n', 
            string.Join('\n', lines[0..81]),
            HrefType,
            ShapeType, 
            TypeType,
            LayoutType,
            AttributeType,
            string.Join('\n', lines[81..103]),
            "      required: true",
            string.Join('\n', lines[103..1242]),
            GetDatasetResponse,
            string.Join('\n', lines[1383..2072]),
            GetAttributesResponse,
            string.Join('\n', lines[2107..2216]),
            GetAttributeResponse,
            string.Join('\n', lines[2244..]));
    }

    private static string[] GetLines(string value)
    {
        var lines = new List<string>();

        using var sr = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(value)));
        string? line;

        while ((line = sr.ReadLine()) != null)
        {
            lines.Add(line);
        }

        return lines.ToArray();
    }

    private static readonly string HrefType = """

        HrefType:
          description: A href.
          type: object
          additionalProperties: false
          properties:
            href:
              description: URL of the resource.
              type: string
            rel:
              description: Relation to this object.
              type: string
    """;


    private static readonly string ShapeType = """

        ShapeType:
          description: A shape.
          type: object
          additionalProperties: false
          properties:
            class:
              description: The shape class.
              type: string
              enum:
                - H5S_NULL
                - H5S_SCALAR
                - H5S_SIMPLE
            dims:
              type: array
              description: The shape dimensions.
              items:
                type: integer
              nullable: true
            maxdims:
              description: The shape maximum dimensions.
              type: array
              items:
                type: number
              nullable: true
          examples:
            - class: H5S_SIMPLE
              dims: [4, 4, 4]
              maxdims: [4, 4, 4]
    """;

    private static readonly string TypeType = """

        TypeType:
          description: A type.
          type: object
          properties:
            class:
              description: The type class.
              type: string
              enum:
                - H5T_COMPOUND
                - H5T_FLOAT
                - H5T_INTEGER
            base:
              description: The base type class.
              type: string
              nullable: true
            fields:
              description: List of fields in a compound dataset.
              type: array
              items:
                type: object
                properties:
                  name:
                    description: Descriptive or identifying name.
                    type: string
                  type:
                    description: The type.
                    oneOf:
                      - $ref: "#/components/schemas/TypeType"
              nullable: true
          examples:
            - base: H5T_STD_U32LE
              class: H5T_INTEGER
    """;

    private static readonly string LayoutType = """

        LayoutType:
          description: A layout.
          type: object
          properties:
            class:
              description: The layout class.
              type: string
            dims:
              description: The chunk dimensions.
              type: array
              items:
                type: integer
              nullable: true
    """;

    private static readonly string AttributeType = """

        AttributeType:
          description: An attribute.
          type: object
          properties:
            created:
              description: The creation date.
              type: number
            lastModified:
              description: The date of last modification.
              type: number
              nullable: true
            name:
              description: The name.
              type: string
            shape:
              description: The shape.
              oneOf:
                - $ref: "#/components/schemas/ShapeType"
            type:
              description: The type.
              oneOf:
                - $ref: "#/components/schemas/TypeType"
            value:
              description: The values.
              nullable: true
            href:
              description: Link to the attribute.
              type: string
              nullable: true
            hrefs:
              description: A collection of relations.
              type: array
              items:
                $ref: "#/components/schemas/HrefType"
              examples:
                - []
              nullable: true
    """;

    private static readonly string GetDatasetResponse = """
                  schema:
                    type: object
                    properties:
                      id:
                        description: UUID of this Dataset.
                        type: string
                        examples:
                          - "d-21ae0bbe-2dea-11e8-9391-0242ac110009"
                      root:
                        description: UUID of root Group in Domain.
                        type: string
                        examples:
                          - "g-d313d498-2de4-11e8-9391-0242ac110009"
                      domain:
                        description: The domain name.
                        type: string
                        examples:
                          - "/home/test_user1/file"
                      created:
                        description: The creation date.
                        type: number
                        examples:
                          - 1521734424.3
                      lastModified:
                        description: The date of the last modification.
                        type: number
                        examples:
                          - 1521734424.3
                      attributeCount:
                        description: The number of attributes.
                        type: number
                        examples:
                          - 0
                      "type":
                        description: The type.
                        oneOf:
                          - $ref: "#/components/schemas/TypeType"
                      shape:
                        description: The shape.
                        oneOf:
                          - $ref: "#/components/schemas/ShapeType"
                      layout:
                        description: The layout.
                        oneOf:
                          - $ref: "#/components/schemas/LayoutType"
                      creationProperties:
                        description: >
                          Dataset creation properties as provided upon creation.
                        type: object
                      hrefs:
                        description: A collection of relations.
                        type: array
                        items:
                          $ref: "#/components/schemas/HrefType"
    """;

    private static readonly string GetAttributeResponse = """
                  schema:
                    $ref: "#/components/schemas/AttributeType"
    """;

    private static readonly string GetAttributesResponse = """
                  schema:
                    type: object
                    description: >
                      A list of attributes.

                    properties:
                      attributes:
                        type: array
                        items:
                          $ref: "#/components/schemas/AttributeType"
                      hrefs:
                        description: A collection of relations.
                        type: array
                        items:
                          $ref: "#/components/schemas/HrefType"
    """;
}