# E-Commerce Domain Example

This example demonstrates how to implement an e-commerce application using Reactive Domain concepts, including CQRS, Event Sourcing, and Process Managers.

## Commands

```csharp
// Command definition
public class PlaceOrder : Command, ICorrelatedMessage
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public List<OrderItem> Items { get; }
    public Address ShippingAddress { get; }
    public PaymentMethod PaymentMethod { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Constructor with validation
    public PlaceOrder(
        Guid orderId,
        Guid customerId,
        List<OrderItem> items,
        Address shippingAddress,
        PaymentMethod paymentMethod,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        // Validate business rules
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order ID is required");
            
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID is required");
            
        if (items == null || !items.Any())
            throw new ArgumentException("Order must contain at least one item");
            
        if (shippingAddress == null)
            throw new ArgumentException("Shipping address is required");
            
        if (paymentMethod == null)
            throw new ArgumentException("Payment method is required");
            
        OrderId = orderId;
        CustomerId = customerId;
        Items = items;
        ShippingAddress = shippingAddress;
        PaymentMethod = paymentMethod;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
    
    // Value objects
    public class OrderItem
    {
        public Guid ProductId { get; }
        public int Quantity { get; }
        public decimal UnitPrice { get; }
        
        public OrderItem(Guid productId, int quantity, decimal unitPrice)
        {
            if (productId == Guid.Empty)
                throw new ArgumentException("Product ID is required");
                
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero");
                
            if (unitPrice <= 0)
                throw new ArgumentException("Unit price must be greater than zero");
                
            ProductId = productId;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }
        
        public decimal GetTotal() => Quantity * UnitPrice;
    }
    
    public class Address
    {
        public string Street { get; }
        public string City { get; }
        public string State { get; }
        public string PostalCode { get; }
        public string Country { get; }
        
        public Address(string street, string city, string state, string postalCode, string country)
        {
            if (string.IsNullOrWhiteSpace(street))
                throw new ArgumentException("Street is required");
                
            if (string.IsNullOrWhiteSpace(city))
                throw new ArgumentException("City is required");
                
            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentException("State is required");
                
            if (string.IsNullOrWhiteSpace(postalCode))
                throw new ArgumentException("Postal code is required");
                
            if (string.IsNullOrWhiteSpace(country))
                throw new ArgumentException("Country is required");
                
            Street = street;
            City = city;
            State = state;
            PostalCode = postalCode;
            Country = country;
        }
    }
    
    public class PaymentMethod
    {
        public PaymentType Type { get; }
        public string CardNumber { get; }
        public string CardHolderName { get; }
        public string ExpirationDate { get; }
        
        public PaymentMethod(PaymentType type, string cardNumber, string cardHolderName, string expirationDate)
        {
            Type = type;
            CardNumber = cardNumber;
            CardHolderName = cardHolderName;
            ExpirationDate = expirationDate;
        }
        
        public enum PaymentType
        {
            CreditCard,
            DebitCard,
            PayPal
        }
    }
}

// Additional commands
public class ProcessPayment : Command, ICorrelatedMessage
{
    public Guid OrderId { get; }
    public decimal Amount { get; }
    public PlaceOrder.PaymentMethod PaymentMethod { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Factory method using MessageBuilder
    public static ProcessPayment Create(Guid orderId, decimal amount, 
        PlaceOrder.PaymentMethod paymentMethod, ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new ProcessPayment(
            orderId, amount, paymentMethod,
            Guid.NewGuid(), source.CorrelationId, source.MsgId));
    }
    
    private ProcessPayment(Guid orderId, decimal amount, PlaceOrder.PaymentMethod paymentMethod,
                          Guid msgId, Guid correlationId, Guid causationId)
    {
        OrderId = orderId;
        Amount = amount;
        PaymentMethod = paymentMethod;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

## Events

```csharp
// Event definition
public class OrderPlaced : Event, ICorrelatedMessage
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public List<OrderItem> Items { get; }
    public Address ShippingAddress { get; }
    public PaymentMethod PaymentMethod { get; }
    public decimal TotalAmount { get; }
    public DateTime OrderDate { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Factory method using MessageBuilder
    public static OrderPlaced Create(
        Guid orderId,
        Guid customerId,
        List<OrderItem> items,
        Address shippingAddress,
        PaymentMethod paymentMethod,
        ICorrelatedMessage source)
    {
        decimal totalAmount = items.Sum(i => i.Quantity * i.UnitPrice);
        
        return MessageBuilder.From(source, () => new OrderPlaced(
            orderId, customerId, items, shippingAddress, paymentMethod, 
            totalAmount, DateTime.UtcNow,
            Guid.NewGuid(), source.CorrelationId, source.MsgId));
    }
    
    private OrderPlaced(
        Guid orderId,
        Guid customerId,
        List<OrderItem> items,
        Address shippingAddress,
        PaymentMethod paymentMethod,
        decimal totalAmount,
        DateTime orderDate,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Items = items;
        ShippingAddress = shippingAddress;
        PaymentMethod = paymentMethod;
        TotalAmount = totalAmount;
        OrderDate = orderDate;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
    
    // Value objects
    public class OrderItem
    {
        public Guid ProductId { get; }
        public int Quantity { get; }
        public decimal UnitPrice { get; }
        
        public OrderItem(Guid productId, int quantity, decimal unitPrice)
        {
            ProductId = productId;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }
    }
    
    public class Address
    {
        public string Street { get; }
        public string City { get; }
        public string State { get; }
        public string PostalCode { get; }
        public string Country { get; }
        
        public Address(string street, string city, string state, string postalCode, string country)
        {
            Street = street;
            City = city;
            State = state;
            PostalCode = postalCode;
            Country = country;
        }
    }
    
    public class PaymentMethod
    {
        public PaymentType Type { get; }
        public string CardNumber { get; }
        public string CardHolderName { get; }
        public string ExpirationDate { get; }
        
        public PaymentMethod(PaymentType type, string cardNumber, string cardHolderName, string expirationDate)
        {
            Type = type;
            CardNumber = cardNumber;
            CardHolderName = cardHolderName;
            ExpirationDate = expirationDate;
        }
        
        public enum PaymentType
        {
            CreditCard,
            DebitCard,
            PayPal
        }
    }
}

// Additional events
public class PaymentProcessed : Event, ICorrelatedMessage
{
    public Guid OrderId { get; }
    public decimal Amount { get; }
    public string TransactionId { get; }
    public DateTime ProcessedDate { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Factory method using MessageBuilder
    public static PaymentProcessed Create(
        Guid orderId,
        decimal amount,
        string transactionId,
        ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new PaymentProcessed(
            orderId, amount, transactionId, DateTime.UtcNow,
            Guid.NewGuid(), source.CorrelationId, source.MsgId));
    }
    
    private PaymentProcessed(
        Guid orderId,
        decimal amount,
        string transactionId,
        DateTime processedDate,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        OrderId = orderId;
        Amount = amount;
        TransactionId = transactionId;
        ProcessedDate = processedDate;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

## Aggregate Implementation

```csharp
public class Order : AggregateRoot
{
    private OrderStatus _status;
    private Guid _customerId;
    private List<OrderItem> _items = new List<OrderItem>();
    private Address _shippingAddress;
    private PaymentMethod _paymentMethod;
    private DateTime _orderDate;
    private decimal _totalAmount;
    private string _paymentTransactionId;
    private string _trackingNumber;
    
    public Order(Guid id) : base(id)
    {
        // Register event handlers
        Register<OrderPlaced>(Apply);
        Register<PaymentProcessed>(Apply);
        Register<OrderShipped>(Apply);
        Register<OrderDelivered>(Apply);
        Register<OrderCancelled>(Apply);
    }
    
    // Command handler methods
    public void PlaceOrder(
        Guid customerId,
        List<PlaceOrder.OrderItem> items,
        PlaceOrder.Address shippingAddress,
        PlaceOrder.PaymentMethod paymentMethod,
        ICorrelatedMessage source)
    {
        // Business rules validation
        if (_status != OrderStatus.None)
            throw new InvalidOperationException("Order has already been placed");
            
        if (items == null || !items.Any())
            throw new ArgumentException("Order must contain at least one item");
            
        // Convert from command value objects to domain value objects
        var orderItems = items.Select(i => new OrderItem(i.ProductId, i.Quantity, i.UnitPrice)).ToList();
        var address = new Address(
            shippingAddress.Street,
            shippingAddress.City,
            shippingAddress.State,
            shippingAddress.PostalCode,
            shippingAddress.Country);
            
        var payment = new PaymentMethod(
            (PaymentMethod.PaymentType)paymentMethod.Type,
            paymentMethod.CardNumber,
            paymentMethod.CardHolderName,
            paymentMethod.ExpirationDate);
            
        // Raise the event using MessageBuilder for correlation
        RaiseEvent(OrderPlaced.Create(
            Id,
            customerId,
            orderItems,
            address,
            payment,
            source));
    }
    
    public void ProcessPayment(string transactionId, ICorrelatedMessage source)
    {
        // Business rules validation
        if (_status != OrderStatus.Placed)
            throw new InvalidOperationException("Order must be in 'Placed' status to process payment");
            
        // Raise the event
        RaiseEvent(PaymentProcessed.Create(
            Id,
            _totalAmount,
            transactionId,
            source));
    }
    
    // Event handler methods
    private void Apply(OrderPlaced @event)
    {
        _status = OrderStatus.Placed;
        _customerId = @event.CustomerId;
        _items = @event.Items.Select(i => new OrderItem(i.ProductId, i.Quantity, i.UnitPrice)).ToList();
        _shippingAddress = new Address(
            @event.ShippingAddress.Street,
            @event.ShippingAddress.City,
            @event.ShippingAddress.State,
            @event.ShippingAddress.PostalCode,
            @event.ShippingAddress.Country);
        _paymentMethod = new PaymentMethod(
            (PaymentMethod.PaymentType)@event.PaymentMethod.Type,
            @event.PaymentMethod.CardNumber,
            @event.PaymentMethod.CardHolderName,
            @event.PaymentMethod.ExpirationDate);
        _orderDate = @event.OrderDate;
        _totalAmount = @event.TotalAmount;
    }
    
    private void Apply(PaymentProcessed @event)
    {
        _status = OrderStatus.PaymentProcessed;
        _paymentTransactionId = @event.TransactionId;
    }
    
    // Additional event handlers...
    
    // Value objects
    public class OrderItem
    {
        public Guid ProductId { get; }
        public int Quantity { get; }
        public decimal UnitPrice { get; }
        
        public OrderItem(Guid productId, int quantity, decimal unitPrice)
        {
            ProductId = productId;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }
        
        public decimal GetTotal() => Quantity * UnitPrice;
    }
    
    public class Address
    {
        public string Street { get; }
        public string City { get; }
        public string State { get; }
        public string PostalCode { get; }
        public string Country { get; }
        
        public Address(string street, string city, string state, string postalCode, string country)
        {
            Street = street;
            City = city;
            State = state;
            PostalCode = postalCode;
            Country = country;
        }
    }
    
    public class PaymentMethod
    {
        public PaymentType Type { get; }
        public string CardNumber { get; }
        public string CardHolderName { get; }
        public string ExpirationDate { get; }
        
        public PaymentMethod(PaymentType type, string cardNumber, string cardHolderName, string expirationDate)
        {
            Type = type;
            CardNumber = cardNumber;
            CardHolderName = cardHolderName;
            ExpirationDate = expirationDate;
        }
        
        public enum PaymentType
        {
            CreditCard,
            DebitCard,
            PayPal
        }
    }
    
    public enum OrderStatus
    {
        None,
        Placed,
        PaymentProcessed,
        Shipped,
        Delivered,
        Cancelled
    }
}
```

## Process Manager for Order Fulfillment

```csharp
public class OrderFulfillmentProcess : 
    IEventHandler<OrderPlaced>,
    IEventHandler<PaymentProcessed>,
    IEventHandler<InventoryReserved>,
    IEventHandler<ShippingArranged>
{
    private readonly ICommandBus _commandBus;
    private readonly ILogger<OrderFulfillmentProcess> _logger;
    
    public OrderFulfillmentProcess(ICommandBus commandBus, ILogger<OrderFulfillmentProcess> logger)
    {
        _commandBus = commandBus;
        _logger = logger;
    }
    
    public void Handle(OrderPlaced @event)
    {
        _logger.LogInformation("Starting fulfillment process for order: {OrderId}", @event.OrderId);
        
        // Process payment
        _commandBus.Send(ProcessPayment.Create(
            @event.OrderId,
            @event.TotalAmount,
            @event.PaymentMethod,
            @event));
    }
    
    public void Handle(PaymentProcessed @event)
    {
        _logger.LogInformation("Payment processed for order: {OrderId}, Transaction: {TransactionId}", 
            @event.OrderId, @event.TransactionId);
        
        // Get the order details
        var order = GetOrderDetails(@event.OrderId);
        
        // Reserve inventory
        _commandBus.Send(MessageBuilder.From(@event, () => new ReserveInventory(
            @event.OrderId,
            order.Items)));
    }
    
    public void Handle(InventoryReserved @event)
    {
        _logger.LogInformation("Inventory reserved for order: {OrderId}", @event.OrderId);
        
        // Get the order details
        var order = GetOrderDetails(@event.OrderId);
        
        // Arrange shipping
        _commandBus.Send(MessageBuilder.From(@event, () => new ArrangeShipping(
            @event.OrderId,
            order.ShippingAddress,
            order.Items)));
    }
    
    public void Handle(ShippingArranged @event)
    {
        _logger.LogInformation("Shipping arranged for order: {OrderId}, Tracking: {TrackingNumber}", 
            @event.OrderId, @event.TrackingNumber);
        
        // Mark order as shipped
        _commandBus.Send(MessageBuilder.From(@event, () => new ShipOrder(
            @event.OrderId,
            @event.TrackingNumber,
            @event.EstimatedDeliveryDate)));
    }
    
    // Helper method to get order details
    private OrderDetails GetOrderDetails(Guid orderId)
    {
        // In a real implementation, this would retrieve the order details from a read model
        // For simplicity, we're returning a placeholder
        return new OrderDetails();
    }
    
    // Helper class for order details
    private class OrderDetails
    {
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public Address ShippingAddress { get; set; } = new Address();
        
        public class OrderItem
        {
            public Guid ProductId { get; set; }
            public int Quantity { get; set; }
        }
        
        public class Address
        {
            public string Street { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string PostalCode { get; set; }
            public string Country { get; set; }
        }
    }
}
```

## Read Model Implementation

```csharp
public class OrderSummaryReadModel : ReadModelBase
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public string PaymentTransactionId { get; set; }
    public string TrackingNumber { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public List<OrderItemSummary> Items { get; set; } = new List<OrderItemSummary>();
    public AddressSummary ShippingAddress { get; set; }
    
    public class OrderItemSummary
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total => Quantity * UnitPrice;
    }
    
    public class AddressSummary
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public string FormattedAddress => $"{Street}, {City}, {State} {PostalCode}, {Country}";
    }
    
    public enum OrderStatus
    {
        Placed,
        PaymentProcessed,
        Shipped,
        Delivered,
        Cancelled
    }
}
```

## Read Model Updater

```csharp
public class OrderSummaryUpdater : 
    IEventHandler<OrderPlaced>,
    IEventHandler<PaymentProcessed>,
    IEventHandler<OrderShipped>,
    IEventHandler<OrderDelivered>,
    IEventHandler<OrderCancelled>
{
    private readonly IReadModelRepository<OrderSummaryReadModel> _readModelRepository;
    private readonly IProductService _productService;
    private readonly ICustomerService _customerService;
    private readonly ILogger<OrderSummaryUpdater> _logger;
    
    public OrderSummaryUpdater(
        IReadModelRepository<OrderSummaryReadModel> readModelRepository,
        IProductService productService,
        ICustomerService customerService,
        ILogger<OrderSummaryUpdater> logger)
    {
        _readModelRepository = readModelRepository;
        _productService = productService;
        _customerService = customerService;
        _logger = logger;
    }
    
    public void Handle(OrderPlaced @event)
    {
        try
        {
            // Get customer details
            var customer = _customerService.GetCustomer(@event.CustomerId);
            
            // Create a new read model
            var readModel = new OrderSummaryReadModel
            {
                Id = @event.OrderId,
                CustomerId = @event.CustomerId,
                CustomerName = customer.Name,
                Status = OrderSummaryReadModel.OrderStatus.Placed,
                TotalAmount = @event.TotalAmount,
                OrderDate = @event.OrderDate,
                ShippingAddress = new OrderSummaryReadModel.AddressSummary
                {
                    Street = @event.ShippingAddress.Street,
                    City = @event.ShippingAddress.City,
                    State = @event.ShippingAddress.State,
                    PostalCode = @event.ShippingAddress.PostalCode,
                    Country = @event.ShippingAddress.Country
                }
            };
            
            // Add order items
            foreach (var item in @event.Items)
            {
                var product = _productService.GetProduct(item.ProductId);
                
                readModel.Items.Add(new OrderSummaryReadModel.OrderItemSummary
                {
                    ProductId = item.ProductId,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });
            }
            
            // Save the read model
            _readModelRepository.Save(readModel);
            
            _logger.LogInformation("Created order summary for order: {OrderId}", @event.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order summary for order: {OrderId}", @event.OrderId);
            throw;
        }
    }
    
    public void Handle(PaymentProcessed @event)
    {
        try
        {
            // Get the read model
            var readModel = _readModelRepository.GetById(@event.OrderId);
            
            // Update the read model
            readModel.Status = OrderSummaryReadModel.OrderStatus.PaymentProcessed;
            readModel.PaymentTransactionId = @event.TransactionId;
            
            // Save the read model
            _readModelRepository.Save(readModel);
            
            _logger.LogInformation("Updated order summary for payment processed: {OrderId}", @event.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order summary for payment processed: {OrderId}", @event.OrderId);
            throw;
        }
    }
    
    // Additional event handlers...
}
```

## API Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IReadModelRepository<OrderSummaryReadModel> _readModelRepository;
    private readonly IProductService _productService;
    
    public OrdersController(
        ICommandBus commandBus,
        IReadModelRepository<OrderSummaryReadModel> readModelRepository,
        IProductService productService)
    {
        _commandBus = commandBus;
        _readModelRepository = readModelRepository;
        _productService = productService;
    }
    
    [HttpPost]
    public IActionResult PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
            
        try
        {
            // Generate a new order ID
            var orderId = Guid.NewGuid();
            
            // Convert request items to command items
            var items = new List<PlaceOrder.OrderItem>();
            foreach (var item in request.Items)
            {
                // Get product details
                var product = _productService.GetProduct(item.ProductId);
                
                items.Add(new PlaceOrder.OrderItem(
                    item.ProductId,
                    item.Quantity,
                    product.Price));
            }
            
            // Create shipping address
            var address = new PlaceOrder.Address(
                request.ShippingAddress.Street,
                request.ShippingAddress.City,
                request.ShippingAddress.State,
                request.ShippingAddress.PostalCode,
                request.ShippingAddress.Country);
                
            // Create payment method
            var paymentMethod = new PlaceOrder.PaymentMethod(
                (PlaceOrder.PaymentMethod.PaymentType)request.PaymentMethod.Type,
                request.PaymentMethod.CardNumber,
                request.PaymentMethod.CardHolderName,
                request.PaymentMethod.ExpirationDate);
                
            // Create a command with correlation
            var command = new PlaceOrder(
                orderId,
                request.CustomerId,
                items,
                address,
                paymentMethod,
                Guid.NewGuid(),
                Guid.NewGuid(), // New correlation ID for this transaction
                Guid.Empty);    // No causation ID for the initial command
                
            // Send the command
            _commandBus.Send(command);
            
            return Accepted(new { OrderId = orderId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    [HttpGet("{id}")]
    public IActionResult GetOrder(Guid id)
    {
        try
        {
            var order = _readModelRepository.GetById(id);
            
            if (order == null)
                return NotFound();
                
            return Ok(order);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    public class PlaceOrderRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        
        [Required]
        [MinLength(1, ErrorMessage = "Order must contain at least one item")]
        public List<OrderItemRequest> Items { get; set; }
        
        [Required]
        public AddressRequest ShippingAddress { get; set; }
        
        [Required]
        public PaymentMethodRequest PaymentMethod { get; set; }
        
        public class OrderItemRequest
        {
            [Required]
            public Guid ProductId { get; set; }
            
            [Required]
            [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero")]
            public int Quantity { get; set; }
        }
        
        public class AddressRequest
        {
            [Required]
            public string Street { get; set; }
            
            [Required]
            public string City { get; set; }
            
            [Required]
            public string State { get; set; }
            
            [Required]
            public string PostalCode { get; set; }
            
            [Required]
            public string Country { get; set; }
        }
        
        public class PaymentMethodRequest
        {
            [Required]
            public int Type { get; set; }
            
            [Required]
            [CreditCard]
            public string CardNumber { get; set; }
            
            [Required]
            public string CardHolderName { get; set; }
            
            [Required]
            [RegularExpression(@"^(0[1-9]|1[0-2])\/([0-9]{2})$", ErrorMessage = "Expiration date must be in format MM/YY")]
            public string ExpirationDate { get; set; }
        }
    }
}
```

## Key Concepts Demonstrated

1. **CQRS Pattern**: Separation of commands (write operations) and queries (read operations)
2. **Event Sourcing**: Using events to represent state changes and reconstruct state
3. **Domain-Driven Design**: Rich domain models with business rules and validations
4. **Value Objects**: Immutable objects that represent concepts in the domain
5. **Process Manager**: Coordinating complex business processes across multiple aggregates
6. **Correlation**: Tracking related messages through the system using `ICorrelatedMessage` and `MessageBuilder`
7. **Repository Pattern**: Using repositories to load and save aggregates
8. **Read Models**: Specialized models for querying data efficiently
9. **Event Handlers**: Components that update read models based on domain events
10. **API Integration**: Exposing the domain through a REST API
