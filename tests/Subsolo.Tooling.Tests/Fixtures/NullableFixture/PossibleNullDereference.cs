namespace NullableFixture;

public static class PossibleNullDereference
{
    public static int LengthOf(string? value)
    {
        return value.Length;
    }
}
