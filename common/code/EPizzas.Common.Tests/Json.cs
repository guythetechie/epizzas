using FluentAssertions;
using FluentAssertions.LanguageExt;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace EPizzas.Common.Tests;

public class JsonNodeExtensionsTests
{
    [Property]
    public Property Clone_returns_copy()
    {
        var arbitrary = Generator.JsonNode.ToArbitrary();

        return Prop.ForAll(arbitrary, source =>
        {
            // Act
            var clone = source.Clone();

            // Assert
            var sourceString = source.ToJsonString();
            var cloneString = clone.ToJsonString();
            cloneString.Should().Be(sourceString);
        });
    }

    [Property]
    public Property TryAsJsonValue_returns_Some_if_node_is_a_json_value()
    {
        var arbitrary = Generator.JsonValue.Select(x => x as JsonNode).ToArbitrary();

        return Prop.ForAll(arbitrary, node =>
        {
            // Act
            var result = node.TryAsJsonValue();

            // Assert
            result.Should().Be(node.AsValue());
        });

    }

    [Property]
    public Property TryAsJsonValue_returns_None_if_node_is_not_a_json_value()
    {
        var arbitrary = Gen.OneOf(Generator.JsonObject.Select(x => x as JsonNode),
                                  Generator.JsonArray.Select(x => x as JsonNode))
                           .OrNull()
                           .ToArbitrary();

        return Prop.ForAll(arbitrary, node =>
        {
            // Act
            var option = node.TryAsJsonValue();

            // Assert
            option.Should().BeNone();
        });
    }

    [Property]
    public Property TryAsJsonObject_returns_Some_if_node_is_a_json_object()
    {
        var arbitrary = Generator.JsonObject.ToArbitrary();

        return Prop.ForAll(arbitrary, node =>
        {
            // Act
            var result = node.TryAsJsonObject();

            // Assert
            result.Should().Be(node.AsObject());
        });

    }

    [Property]
    public Property TryAsJsonObject_returns_None_if_node_is_not_a_json_object()
    {
        var arbitrary = Gen.OneOf(Generator.JsonValue.Select(x => x as JsonNode),
                                  Generator.JsonArray.Select(x => x as JsonNode))
                           .OrNull()
                           .ToArbitrary();

        return Prop.ForAll(arbitrary, node =>
        {
            // Act
            var option = node.TryAsJsonObject();

            // Assert
            option.Should().BeNone();
        });
    }

    [Property]
    public Property TryAsJsonArray_returns_Some_if_node_is_a_json_array()
    {
        var arbitrary = Generator.JsonArray.ToArbitrary();

        return Prop.ForAll(arbitrary, node =>
        {
            // Act
            var option = node.TryAsJsonArray();

            // Assert
            option.Should().Be(node.AsArray());
        });

    }

    [Property]
    public Property TryAsJsonArray_returns_None_if_node_is_not_a_json_array()
    {
        var arbitrary = Gen.OneOf(Generator.JsonValue.Select(x => x as JsonNode),
                                  Generator.JsonObject.Select(x => x as JsonNode))
                           .OrNull()
                           .ToArbitrary();

        return Prop.ForAll(arbitrary, node =>
        {
            // Act
            var option = node.TryAsJsonArray();

            // Assert
            option.Should().BeNone();
        });
    }
}

public class JsonObjectExtensionsTests
{
    [Property]
    public Property GetProperty_returns_property_JsonNode()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonNode
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyValue, jsonObject) = input;

            // Act
            var actualPropertyValue = jsonObject.GetProperty(propertyName);

            // Assert
            actualPropertyValue.ToJsonString().Should().Be(expectedPropertyValue.ToJsonString());
        });
    }

    [Property]
    public Property TryGetProperty_returns_Some_if_the_property_exists()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonNode
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyValue, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetProperty(propertyName);

            // Assert
            result.Should().BeRight(value => value.ToJsonString().Should().Be(expectedPropertyValue.ToJsonString()));
        });
    }

    [Fact]
    public void TryGetProperty_returns_None_if_the_property_does_not_exist()
    {
        // Arrange
        var jsonObject = new JsonObject();

        // Act
        var result = jsonObject.TryGetProperty("nonExistingProperty");

        // Assert
        result.Should().BeLeft();
    }

    [Property]
    public Property GetJsonObjectProperty_returns_property_JsonObject()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonObject
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var actualPropertyValue = jsonObject.GetJsonObjectProperty(propertyName);

            // Assert
            actualPropertyValue.ToJsonString().Should().Be(node.ToJsonString());
        });
    }

    [Property]
    public Property TryGetJsonObjectProperty_returns_Some_if_the_property_is_a_json_object()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonObject
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetJsonObjectProperty(propertyName);

            // Assert
            result.Should().BeRight(value => value.ToJsonString().Should().Be(node.ToJsonString()));
        });
    }

    [Property]
    public Property TryGetJsonObjectProperty_returns_None_if_the_property_is_not_a_json_object()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Gen.OneOf(Generator.JsonValue.Select(x => x as JsonNode),
                                               Generator.JsonArray.Select(x => x as JsonNode))
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyValue, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetJsonObjectProperty(propertyName);

            // Assert
            result.Should().BeLeft();
        });
    }

    [Property]
    public Property GetJsonArrayProperty_returns_property_JsonArray()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonArray
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var actualPropertyValue = jsonObject.GetJsonArrayProperty(propertyName);

            // Assert
            actualPropertyValue.ToJsonString().Should().Be(node.ToJsonString());
        });
    }

    [Property]
    public Property TryGetJsonArrayProperty_returns_Some_if_the_property_is_a_JsonArray()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonArray
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetJsonArrayProperty(propertyName);

            // Assert
            result.Should().BeRight(value => value.ToJsonString().Should().Be(node.ToJsonString()));
        });
    }

    [Property]
    public Property TryGetJsonArrayProperty_returns_None_if_the_property_is_not_a_JsonArray()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Gen.OneOf(Generator.JsonValue.Select(x => x as JsonNode),
                                               Generator.JsonObject.Select(x => x as JsonNode))
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyValue, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetJsonArrayProperty(propertyName);

            // Assert
            result.Should().BeLeft();
        });
    }

    [Property]
    public Property GetJsonObjectArrayProperty_returns_property_JsonObjectArray()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonObject
                                              .ListOf()
                                              .Select(list => list.ToJsonArray())
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var jsonObjects = jsonObject.GetJsonObjectArrayProperty(propertyName);

            // Assert
            jsonObjects.Should().Equal(node, (expected, actual) => expected.ToJsonString() == actual!.ToJsonString());
        });
    }

    [Property]
    public Property TryGetJsonObjectArrayProperty_returns_Some_if_the_property_is_a_JsonObjectArray()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonObject
                                              .ListOf()
                                              .Select(list => list.ToJsonArray())
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetJsonObjectArrayProperty(propertyName);

            // Assert
            result.Should().BeRight(value => value.Should().Equal(node, (expected, actual) => expected.ToJsonString() == actual!.ToJsonString()));
        });
    }

    [Property]
    public Property TryGetJsonObjectArrayProperty_returns_None_if_the_property_is_not_a_JsonObjectArray()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Gen.OneOf(Generator.JsonValue.Select(x => x as JsonNode),
                                               Generator.JsonObject.Select(x => x as JsonNode),
                                               Generator.JsonArray
                                                        .Select(x => x as JsonNode)
                                                        .Where(node => node.AsArray()
                                                                           .Any(arrayMember => arrayMember is not JsonObject)))
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyValue, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetJsonObjectArrayProperty(propertyName);

            // Assert
            result.Should().BeLeft();
        });
    }

    [Property]
    public Property GetJsonValueProperty_returns_property_JsonValue()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonValue
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var actualPropertyValue = jsonObject.GetJsonValueProperty(propertyName);

            // Assert
            actualPropertyValue.ToJsonString().Should().Be(node.ToJsonString());
        });
    }

    [Property]
    public Property TryGetJsonValueProperty_returns_Some_if_the_property_is_a_JsonValue()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonValue
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetJsonValueProperty(propertyName);

            // Assert
            result.Should().BeRight(value => value.ToJsonString().Should().Be(node.ToJsonString()));
        });
    }

    [Property]
    public Property TryGetJsonValueProperty_returns_None_if_the_property_is_not_a_JsonValue()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Gen.OneOf(Generator.JsonArray.Select(x => x as JsonNode), Generator.JsonObject.Select(x => x as JsonNode))
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyValue, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetJsonValueProperty(propertyName);

            // Assert
            result.Should().BeLeft();
        });
    }

    [Property]
    public Property GetJsonStringProperty_returns_property_string()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.GenerateJsonValue<string>()
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var value = jsonObject.GetStringProperty(propertyName);

            // Assert
            value.Should().Be(node.GetValue<string>());
        });
    }

    [Property]
    public Property TryGetJsonStringProperty_returns_Some_if_the_property_is_a_string()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.GenerateJsonValue<string>()
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetStringProperty(propertyName);

            // Assert
            result.Should().BeRight(value => value.Should().Be(node.GetValue<string>()));
        });
    }

    [Property]
    public Property TryGetJsonStringProperty_returns_None_if_the_property_is_not_a_string()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Gen.OneOf(Generator.JsonArray.Select(x => x as JsonNode),
                                               Generator.JsonObject.Select(x => x as JsonNode),
                                               Generator.JsonValue
                                                        .Where(jsonValue => jsonValue.TryGetValue<string>(out var _) is false)
                                                        .Select(x => x as JsonNode))
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyString, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetStringProperty(propertyName);

            // Assert
            result.Should().BeLeft();
        });
    }

    [Property]
    public Property GetJsonBoolProperty_returns_property_bool()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.GenerateJsonValue<bool>()
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var value = jsonObject.GetBoolProperty(propertyName);

            // Assert
            value.Should().Be(node.GetValue<bool>());
        });
    }

    [Property]
    public Property TryGetJsonBoolProperty_returns_Some_if_the_property_is_a_bool()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.GenerateJsonValue<bool>()
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetBoolProperty(propertyName);

            // Assert
            result.Should().BeRight();
        });
    }

    [Property]
    public Property TryGetJsonBoolProperty_returns_None_if_the_property_is_not_a_bool()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Gen.OneOf(Generator.JsonArray.Select(x => x as JsonNode),
                                               Generator.JsonObject.Select(x => x as JsonNode),
                                               Generator.JsonValue
                                                        .Where(jsonValue => jsonValue.TryGetValue<bool>(out var _) is false)
                                                        .Select(x => x as JsonNode))
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyBool, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetBoolProperty(propertyName);

            // Assert
            result.Should().BeLeft();
        });
    }

    [Property]
    public Property GetJsonIntProperty_returns_property_int()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.GenerateJsonValue<int>()
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var value = jsonObject.GetIntProperty(propertyName);

            // Assert
            value.Should().Be(node.GetValue<int>());
        });
    }

    [Property]
    public Property TryGetJsonIntProperty_returns_Some_if_the_property_is_a_int()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.GenerateJsonValue<int>()
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetIntProperty(propertyName);

            // Assert
            result.Should().Be(node.GetValue<int>());
        });
    }

    [Property]
    public Property TryGetJsonIntProperty_returns_None_if_the_property_is_not_a_int()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Gen.OneOf(Generator.JsonArray.Select(x => x as JsonNode),
                                               Generator.JsonObject.Select(x => x as JsonNode),
                                               Generator.JsonValue
                                                        .Where(jsonValue => jsonValue.TryGetValue<int>(out var _) is false)
                                                        .Select(x => x as JsonNode))
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyInt, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetIntProperty(propertyName);

            // Assert
            result.Should().BeLeft();
        });
    }

    [Property]
    public Property GetJsonDoubleProperty_returns_property_double()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.GenerateJsonValue<double>()
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var value = jsonObject.GetDoubleProperty(propertyName);

            // Assert
            value.Should().Be(node.GetValue<double>());
        });
    }

    [Property]
    public Property TryGetJsonDoubleProperty_returns_Some_if_the_property_is_a_double()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.GenerateJsonValue<double>()
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetDoubleProperty(propertyName);

            // Assert
            result.Should().Be(node.GetValue<double>());
        });
    }

    [Property]
    public Property TryGetJsonDoubleProperty_returns_None_if_the_property_is_not_a_double()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Gen.OneOf(Generator.JsonArray.Select(x => x as JsonNode),
                                               Generator.JsonObject.Select(x => x as JsonNode),
                                               Generator.JsonValue
                                                        .Where(jsonValue => jsonValue.TryGetValue<double>(out var _) is false)
                                                        .Select(x => x as JsonNode))
                        let jsonObject = new JsonObject { [propertyName] = node }
                        select (propertyName, node, jsonObject);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, expectedPropertyDouble, jsonObject) = input;

            // Act
            var result = jsonObject.TryGetDoubleProperty(propertyName);

            // Assert
            result.Should().BeLeft();
        });
    }

    [Property]
    public Property AddProperty_adds_property()
    {
        var generator = from propertyName in Generator.GenerateDefault<string>()
                        from node in Generator.JsonNode.OrNull()
                        select (propertyName, node);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            // Arrange
            var (propertyName, node) = input;
            var jsonObject = new JsonObject();

            // Act
            jsonObject = jsonObject.AddProperty(propertyName, node);

            // Assert
            var addedNode = jsonObject.Should().ContainKey(propertyName).WhoseValue;

            switch (addedNode, node)
            {
                case (null, null): break;
                case (not null, not null): addedNode.ToJsonString().Should().Be(node.ToJsonString()); break;
                case (not null, null):
                case (null, not null): throw new InvalidOperationException("Nullability must match");
            }
        });
    }
}