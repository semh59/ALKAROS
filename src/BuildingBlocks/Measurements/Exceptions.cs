namespace ALKAROS.Measurements;

public class UnitException : Exception
{
    public UnitException(string message) : base(message) { }
    public UnitException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class IncompatibleUnitDimensionException : UnitException
{
    public string FromUnitCode { get; }
    public UnitDimension FromDimension { get; }
    public string ToUnitCode { get; }
    public UnitDimension ToDimension { get; }

    public IncompatibleUnitDimensionException(string fromUnitCode, UnitDimension fromDim, string toUnitCode, UnitDimension toDim)
        : base($"Cannot convert from '{fromUnitCode}' ({fromDim}) to '{toUnitCode}' ({toDim}): cross-dimension conversion without bridge conversion rule is prohibited.")
    {
        FromUnitCode = fromUnitCode;
        FromDimension = fromDim;
        ToUnitCode = toUnitCode;
        ToDimension = toDim;
    }
}

public sealed class ContradictoryUnitConversionException : UnitException
{
    public ContradictoryUnitConversionException(string message) : base(message) { }
}

public sealed class InvalidUnitConversionFactorException : UnitException
{
    public InvalidUnitConversionFactorException(string message) : base(message) { }
}

public sealed class UnknownUnitException : UnitException
{
    public string UnitCode { get; }

    public UnknownUnitException(string unitCode)
        : base($"Measurement unit '{unitCode}' is not registered.")
    {
        UnitCode = unitCode;
    }
}

public sealed class InvalidUnitQuantityException : UnitException
{
    public InvalidUnitQuantityException(string message) : base(message) { }
}
