namespace Shared.Application.Common;

public static class Messages
{
    public static class Products
    {
        public const string NameRequired = "Product name is required.";
        public const string PriceMustBePositive = "Product price must be greater than zero.";
        public const string StockCannotBeNegative = "Product stock cannot be negative.";
        public const string DuplicateName = "A product with the same name already exists.";
        public const string NotFound = "Product not found.";
        public const string AlreadyActive = "Product is already active.";
        public const string Created = "Created";
        public const string Deleted = "Deleted";
        public const string Restored = "Restored";
    }

    public static class Authorization
    {
        public const string Unauthorized = "Unauthorized";
    }

    public static class Subscription
    {
        public const string CreateQuotaExceeded = "Create quota exceeded for current plan.";
    }
}
