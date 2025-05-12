# Inventory Management Example

This example demonstrates how to implement an inventory management system using Reactive Domain concepts, including CQRS, Event Sourcing, and Sagas.

## Commands

```csharp
// Command definition
public class ReceiveInventory : Command, ICorrelatedMessage
{
    public Guid ProductId { get; }
    public int Quantity { get; }
    public string BatchNumber { get; }
    public string Location { get; }
    public DateTime ExpirationDate { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    public ReceiveInventory(
        Guid productId,
        int quantity,
        string batchNumber,
        string location,
        DateTime expirationDate,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        // Validate business rules
        if (productId == Guid.Empty)
            throw new ArgumentException("Product ID is required");
            
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero");
            
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new ArgumentException("Batch number is required");
            
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location is required");
            
        if (expirationDate <= DateTime.UtcNow)
            throw new ArgumentException("Expiration date must be in the future");
            
        ProductId = productId;
        Quantity = quantity;
        BatchNumber = batchNumber;
        Location = location;
        ExpirationDate = expirationDate;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}

public class AllocateInventory : Command, ICorrelatedMessage
{
    public Guid OrderId { get; }
    public Guid ProductId { get; }
    public int Quantity { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    public AllocateInventory(
        Guid orderId,
        Guid productId,
        int quantity,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        // Validate business rules
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order ID is required");
            
        if (productId == Guid.Empty)
            throw new ArgumentException("Product ID is required");
            
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero");
            
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
    
    // Factory method using MessageBuilder
    public static AllocateInventory Create(
        Guid orderId,
        Guid productId,
        int quantity,
        ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new AllocateInventory(
            orderId, productId, quantity,
            Guid.NewGuid(), source.CorrelationId, source.MsgId));
    }
}
```

## Events

```csharp
public class InventoryReceived : Event, ICorrelatedMessage
{
    public Guid ProductId { get; }
    public int Quantity { get; }
    public string BatchNumber { get; }
    public string Location { get; }
    public DateTime ExpirationDate { get; }
    public DateTime ReceivedDate { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Factory method using MessageBuilder
    public static InventoryReceived Create(
        Guid productId,
        int quantity,
        string batchNumber,
        string location,
        DateTime expirationDate,
        ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new InventoryReceived(
            productId, quantity, batchNumber, location, expirationDate, DateTime.UtcNow,
            Guid.NewGuid(), source.CorrelationId, source.MsgId));
    }
    
    private InventoryReceived(
        Guid productId,
        int quantity,
        string batchNumber,
        string location,
        DateTime expirationDate,
        DateTime receivedDate,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        ProductId = productId;
        Quantity = quantity;
        BatchNumber = batchNumber;
        Location = location;
        ExpirationDate = expirationDate;
        ReceivedDate = receivedDate;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}

public class InventoryAllocated : Event, ICorrelatedMessage
{
    public Guid OrderId { get; }
    public Guid ProductId { get; }
    public int Quantity { get; }
    public List<AllocationDetail> AllocationDetails { get; }
    public DateTime AllocationDate { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    public class AllocationDetail
    {
        public string BatchNumber { get; }
        public string Location { get; }
        public int Quantity { get; }
        
        public AllocationDetail(string batchNumber, string location, int quantity)
        {
            BatchNumber = batchNumber;
            Location = location;
            Quantity = quantity;
        }
    }
    
    // Factory method using MessageBuilder
    public static InventoryAllocated Create(
        Guid orderId,
        Guid productId,
        int quantity,
        List<AllocationDetail> allocationDetails,
        ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new InventoryAllocated(
            orderId, productId, quantity, allocationDetails, DateTime.UtcNow,
            Guid.NewGuid(), source.CorrelationId, source.MsgId));
    }
    
    private InventoryAllocated(
        Guid orderId,
        Guid productId,
        int quantity,
        List<AllocationDetail> allocationDetails,
        DateTime allocationDate,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        AllocationDetails = allocationDetails;
        AllocationDate = allocationDate;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

## Aggregate Implementation

```csharp
public class InventoryItem : AggregateRoot
{
    private string _productName;
    private string _sku;
    private List<InventoryBatch> _batches = new List<InventoryBatch>();
    private int _availableQuantity;
    private int _allocatedQuantity;
    
    public InventoryItem(Guid id) : base(id)
    {
        // Register event handlers
        Register<InventoryItemCreated>(Apply);
        Register<InventoryReceived>(Apply);
        Register<InventoryAllocated>(Apply);
        Register<InventoryReleased>(Apply);
        Register<InventoryShipped>(Apply);
    }
    
    // Command handler methods
    public void ReceiveInventory(
        int quantity,
        string batchNumber,
        string location,
        DateTime expirationDate,
        ICorrelatedMessage source)
    {
        // Business rules validation
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero");
            
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new ArgumentException("Batch number is required");
            
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location is required");
            
        if (expirationDate <= DateTime.UtcNow)
            throw new ArgumentException("Expiration date must be in the future");
            
        // Check for duplicate batch
        if (_batches.Any(b => b.BatchNumber == batchNumber))
            throw new InvalidOperationException($"Batch {batchNumber} already exists");
            
        // Raise the event
        RaiseEvent(InventoryReceived.Create(
            Id, quantity, batchNumber, location, expirationDate, source));
    }
    
    public void AllocateInventory(
        Guid orderId,
        int requestedQuantity,
        ICorrelatedMessage source)
    {
        // Business rules validation
        if (requestedQuantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero");
            
        if (_availableQuantity < requestedQuantity)
            throw new InsufficientInventoryException($"Insufficient inventory. Available: {_availableQuantity}, Requested: {requestedQuantity}");
            
        // Allocate inventory using FIFO (First In, First Out)
        var allocationDetails = new List<InventoryAllocated.AllocationDetail>();
        var remainingToAllocate = requestedQuantity;
        
        // Sort batches by expiration date (FEFO - First Expired, First Out)
        var sortedBatches = _batches
            .Where(b => b.AvailableQuantity > 0)
            .OrderBy(b => b.ExpirationDate)
            .ToList();
            
        foreach (var batch in sortedBatches)
        {
            if (remainingToAllocate <= 0)
                break;
                
            var quantityToAllocate = Math.Min(batch.AvailableQuantity, remainingToAllocate);
            
            allocationDetails.Add(new InventoryAllocated.AllocationDetail(
                batch.BatchNumber, batch.Location, quantityToAllocate));
                
            remainingToAllocate -= quantityToAllocate;
        }
        
        // Raise the event
        RaiseEvent(InventoryAllocated.Create(
            orderId, Id, requestedQuantity, allocationDetails, source));
    }
    
    // Event handler methods
    private void Apply(InventoryReceived @event)
    {
        // Add new batch
        _batches.Add(new InventoryBatch(
            @event.BatchNumber,
            @event.Location,
            @event.Quantity,
            0,
            @event.ExpirationDate));
            
        // Update available quantity
        _availableQuantity += @event.Quantity;
    }
    
    private void Apply(InventoryAllocated @event)
    {
        // Update batches
        foreach (var detail in @event.AllocationDetails)
        {
            var batch = _batches.First(b => b.BatchNumber == detail.BatchNumber);
            batch.AllocateQuantity(detail.Quantity);
        }
        
        // Update quantities
        _availableQuantity -= @event.Quantity;
        _allocatedQuantity += @event.Quantity;
    }
    
    // Helper class for inventory batches
    private class InventoryBatch
    {
        public string BatchNumber { get; }
        public string Location { get; }
        public int TotalQuantity { get; }
        public int AllocatedQuantity { get; private set; }
        public DateTime ExpirationDate { get; }
        
        public int AvailableQuantity => TotalQuantity - AllocatedQuantity;
        
        public InventoryBatch(
            string batchNumber,
            string location,
            int totalQuantity,
            int allocatedQuantity,
            DateTime expirationDate)
        {
            BatchNumber = batchNumber;
            Location = location;
            TotalQuantity = totalQuantity;
            AllocatedQuantity = allocatedQuantity;
            ExpirationDate = expirationDate;
        }
        
        public void AllocateQuantity(int quantity)
        {
            if (quantity > AvailableQuantity)
                throw new InvalidOperationException($"Cannot allocate {quantity} from batch {BatchNumber}. Only {AvailableQuantity} available.");
                
            AllocatedQuantity += quantity;
        }
    }
    
    // Custom exception
    public class InsufficientInventoryException : Exception
    {
        public InsufficientInventoryException(string message) : base(message)
        {
        }
    }
}
```

## Command Handler Implementation

```csharp
public class ReceiveInventoryHandler : ICommandHandler<ReceiveInventory>
{
    private readonly ICorrelatedRepository _repository;
    private readonly IProductService _productService;
    private readonly ILogger<ReceiveInventoryHandler> _logger;
    
    public ReceiveInventoryHandler(
        ICorrelatedRepository repository,
        IProductService productService,
        ILogger<ReceiveInventoryHandler> logger)
    {
        _repository = repository;
        _productService = productService;
        _logger = logger;
    }
    
    public void Handle(ReceiveInventory command)
    {
        _logger.LogInformation("Processing inventory receipt: {Quantity} of {ProductId}, Batch: {BatchNumber}",
            command.Quantity, command.ProductId, command.BatchNumber);
            
        try
        {
            // Check if product exists
            var product = _productService.GetProduct(command.ProductId);
            
            // Get or create inventory item
            InventoryItem inventoryItem;
            if (!_repository.TryGetById(command.ProductId, out inventoryItem, command))
            {
                // Create new inventory item
                inventoryItem = new InventoryItem(command.ProductId);
                
                // Initialize with product details
                var createEvent = MessageBuilder.From(command, () => new InventoryItemCreated(
                    command.ProductId,
                    product.Name,
                    product.SKU,
                    DateTime.UtcNow));
                    
                inventoryItem.Initialize(createEvent);
            }
            
            // Receive inventory
            inventoryItem.ReceiveInventory(
                command.Quantity,
                command.BatchNumber,
                command.Location,
                command.ExpirationDate,
                command);
                
            // Save the inventory item
            _repository.Save(inventoryItem);
            
            _logger.LogInformation("Inventory receipt processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing inventory receipt: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}
```

## Saga Implementation for Order Fulfillment

```csharp
public class OrderFulfillmentSaga : 
    IEventHandler<OrderPlaced>,
    IEventHandler<InventoryAllocated>,
    IEventHandler<PaymentProcessed>
{
    private readonly ICommandBus _commandBus;
    private readonly ISagaRepository<OrderFulfillmentState> _sagaRepository;
    private readonly ILogger<OrderFulfillmentSaga> _logger;
    
    public OrderFulfillmentSaga(
        ICommandBus commandBus,
        ISagaRepository<OrderFulfillmentState> sagaRepository,
        ILogger<OrderFulfillmentSaga> logger)
    {
        _commandBus = commandBus;
        _sagaRepository = sagaRepository;
        _logger = logger;
    }
    
    public void Handle(OrderPlaced @event)
    {
        _logger.LogInformation("Starting order fulfillment saga for order: {OrderId}", @event.OrderId);
        
        // Create a new saga state
        var sagaState = new OrderFulfillmentState
        {
            Id = @event.OrderId,
            OrderId = @event.OrderId,
            CustomerId = @event.CustomerId,
            TotalAmount = @event.TotalAmount,
            Status = OrderFulfillmentStatus.Started,
            Items = @event.Items.Select(i => new OrderFulfillmentState.OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                IsAllocated = false
            }).ToList()
        };
        
        // Save the saga state
        _sagaRepository.Save(sagaState);
        
        // Start allocating inventory for each item
        foreach (var item in @event.Items)
        {
            _commandBus.Send(AllocateInventory.Create(
                @event.OrderId,
                item.ProductId,
                item.Quantity,
                @event));
        }
        
        // Process payment
        _commandBus.Send(MessageBuilder.From(@event, () => new ProcessPayment(
            @event.OrderId,
            @event.CustomerId,
            @event.PaymentMethod,
            @event.TotalAmount)));
    }
    
    public void Handle(InventoryAllocated @event)
    {
        _logger.LogInformation("Inventory allocated for order: {OrderId}, Product: {ProductId}",
            @event.OrderId, @event.ProductId);
            
        // Get the saga state
        var sagaState = _sagaRepository.GetById(@event.OrderId);
        
        // Update the item allocation status
        var item = sagaState.Items.First(i => i.ProductId == @event.ProductId);
        item.IsAllocated = true;
        
        // Check if all items are allocated
        if (sagaState.Items.All(i => i.IsAllocated) && sagaState.IsPaymentProcessed)
        {
            sagaState.Status = OrderFulfillmentStatus.ReadyForShipment;
            
            // Send command to prepare shipment
            _commandBus.Send(MessageBuilder.From(@event, () => new PrepareShipment(
                @event.OrderId,
                sagaState.ShippingAddress,
                sagaState.Items.Select(i => new PrepareShipment.ShipmentItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList())));
        }
        
        // Save the updated saga state
        _sagaRepository.Save(sagaState);
    }
    
    public void Handle(PaymentProcessed @event)
    {
        _logger.LogInformation("Payment processed for order: {OrderId}", @event.OrderId);
        
        // Get the saga state
        var sagaState = _sagaRepository.GetById(@event.OrderId);
        
        // Update payment status
        sagaState.IsPaymentProcessed = true;
        sagaState.PaymentTransactionId = @event.TransactionId;
        
        // Check if all items are allocated
        if (sagaState.Items.All(i => i.IsAllocated) && sagaState.IsPaymentProcessed)
        {
            sagaState.Status = OrderFulfillmentStatus.ReadyForShipment;
            
            // Send command to prepare shipment
            _commandBus.Send(MessageBuilder.From(@event, () => new PrepareShipment(
                @event.OrderId,
                sagaState.ShippingAddress,
                sagaState.Items.Select(i => new PrepareShipment.ShipmentItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList())));
        }
        
        // Save the updated saga state
        _sagaRepository.Save(sagaState);
    }
    
    // Saga state
    public class OrderFulfillmentState
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public OrderFulfillmentStatus Status { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public bool IsPaymentProcessed { get; set; }
        public string PaymentTransactionId { get; set; }
        public Address ShippingAddress { get; set; }
        
        public class OrderItem
        {
            public Guid ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public bool IsAllocated { get; set; }
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
    
    public enum OrderFulfillmentStatus
    {
        Started,
        InventoryAllocated,
        PaymentProcessed,
        ReadyForShipment,
        Shipped,
        Delivered,
        Cancelled
    }
}
```

## Read Model Implementation

```csharp
public class InventorySummaryReadModel : ReadModelBase
{
    public string ProductName { get; set; }
    public string SKU { get; set; }
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int AllocatedQuantity { get; set; }
    public List<BatchSummary> Batches { get; set; } = new List<BatchSummary>();
    public DateTime LastUpdated { get; set; }
    
    public class BatchSummary
    {
        public string BatchNumber { get; set; }
        public string Location { get; set; }
        public int TotalQuantity { get; set; }
        public int AvailableQuantity { get; set; }
        public int AllocatedQuantity { get; set; }
        public DateTime ExpirationDate { get; set; }
        public DateTime ReceivedDate { get; set; }
    }
}
```

## Read Model Updater

```csharp
public class InventorySummaryUpdater : 
    IEventHandler<InventoryItemCreated>,
    IEventHandler<InventoryReceived>,
    IEventHandler<InventoryAllocated>,
    IEventHandler<InventoryShipped>
{
    private readonly IReadModelRepository<InventorySummaryReadModel> _readModelRepository;
    private readonly ILogger<InventorySummaryUpdater> _logger;
    
    public InventorySummaryUpdater(
        IReadModelRepository<InventorySummaryReadModel> readModelRepository,
        ILogger<InventorySummaryUpdater> logger)
    {
        _readModelRepository = readModelRepository;
        _logger = logger;
    }
    
    public void Handle(InventoryItemCreated @event)
    {
        try
        {
            // Create a new read model
            var readModel = new InventorySummaryReadModel
            {
                Id = @event.ProductId,
                ProductName = @event.ProductName,
                SKU = @event.SKU,
                TotalQuantity = 0,
                AvailableQuantity = 0,
                AllocatedQuantity = 0,
                LastUpdated = @event.CreatedDate
            };
            
            // Save the read model
            _readModelRepository.Save(readModel);
            
            _logger.LogInformation("Created inventory summary for product: {ProductId}", @event.ProductId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inventory summary for product: {ProductId}", @event.ProductId);
            throw;
        }
    }
    
    public void Handle(InventoryReceived @event)
    {
        try
        {
            // Get the read model
            var readModel = _readModelRepository.GetById(@event.ProductId);
            
            // Update quantities
            readModel.TotalQuantity += @event.Quantity;
            readModel.AvailableQuantity += @event.Quantity;
            readModel.LastUpdated = @event.ReceivedDate;
            
            // Add batch
            readModel.Batches.Add(new InventorySummaryReadModel.BatchSummary
            {
                BatchNumber = @event.BatchNumber,
                Location = @event.Location,
                TotalQuantity = @event.Quantity,
                AvailableQuantity = @event.Quantity,
                AllocatedQuantity = 0,
                ExpirationDate = @event.ExpirationDate,
                ReceivedDate = @event.ReceivedDate
            });
            
            // Save the updated read model
            _readModelRepository.Save(readModel);
            
            _logger.LogInformation("Updated inventory summary for received inventory: {ProductId}, Batch: {BatchNumber}",
                @event.ProductId, @event.BatchNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory summary for received inventory: {ProductId}", @event.ProductId);
            throw;
        }
    }
    
    public void Handle(InventoryAllocated @event)
    {
        try
        {
            // Get the read model
            var readModel = _readModelRepository.GetById(@event.ProductId);
            
            // Update quantities
            readModel.AvailableQuantity -= @event.Quantity;
            readModel.AllocatedQuantity += @event.Quantity;
            readModel.LastUpdated = @event.AllocationDate;
            
            // Update batches
            foreach (var detail in @event.AllocationDetails)
            {
                var batch = readModel.Batches.First(b => b.BatchNumber == detail.BatchNumber);
                batch.AvailableQuantity -= detail.Quantity;
                batch.AllocatedQuantity += detail.Quantity;
            }
            
            // Save the updated read model
            _readModelRepository.Save(readModel);
            
            _logger.LogInformation("Updated inventory summary for allocated inventory: {ProductId}, Order: {OrderId}",
                @event.ProductId, @event.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory summary for allocated inventory: {ProductId}", @event.ProductId);
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
public class InventoryController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IReadModelRepository<InventorySummaryReadModel> _readModelRepository;
    
    public InventoryController(
        ICommandBus commandBus,
        IReadModelRepository<InventorySummaryReadModel> readModelRepository)
    {
        _commandBus = commandBus;
        _readModelRepository = readModelRepository;
    }
    
    [HttpPost("receive")]
    public IActionResult ReceiveInventory([FromBody] ReceiveInventoryRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
            
        try
        {
            // Create a command with correlation
            var command = new ReceiveInventory(
                request.ProductId,
                request.Quantity,
                request.BatchNumber,
                request.Location,
                request.ExpirationDate,
                Guid.NewGuid(),
                Guid.NewGuid(), // New correlation ID for this transaction
                Guid.Empty);    // No causation ID for the initial command
                
            // Send the command
            _commandBus.Send(command);
            
            return Accepted(new { TransactionId = command.MsgId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    [HttpGet("{id}")]
    public IActionResult GetInventory(Guid id)
    {
        try
        {
            var inventory = _readModelRepository.GetById(id);
            
            if (inventory == null)
                return NotFound();
                
            return Ok(inventory);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    [HttpGet("low-stock")]
    public IActionResult GetLowStockItems([FromQuery] int threshold = 10)
    {
        try
        {
            var lowStockItems = _readModelRepository.FindAll()
                .Where(i => i.AvailableQuantity < threshold)
                .ToList();
                
            return Ok(lowStockItems);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    public class ReceiveInventoryRequest
    {
        [Required]
        public Guid ProductId { get; set; }
        
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero")]
        public int Quantity { get; set; }
        
        [Required]
        public string BatchNumber { get; set; }
        
        [Required]
        public string Location { get; set; }
        
        [Required]
        public DateTime ExpirationDate { get; set; }
    }
}
```

## Key Concepts Demonstrated

1. **CQRS Pattern**: Separation of commands (write operations) and queries (read operations)
2. **Event Sourcing**: Using events to represent state changes and reconstruct state
3. **Domain-Driven Design**: Rich domain models with business rules and validations
4. **Value Objects**: Immutable objects that represent concepts in the domain
5. **Sagas**: Long-running processes that coordinate multiple aggregates
6. **Correlation**: Tracking related messages through the system using `ICorrelatedMessage` and `MessageBuilder`
7. **Repository Pattern**: Using repositories to load and save aggregates
8. **Read Models**: Specialized models for querying data efficiently
9. **Event Handlers**: Components that update read models based on domain events
10. **API Integration**: Exposing the domain through a REST API
