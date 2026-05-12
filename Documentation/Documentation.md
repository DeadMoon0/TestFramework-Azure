# TestFramework.Azure

An Azure extension package for TestFramework.

## Azure Component
Every azure component is Identified by an Identifier. The Identifier is used to select a Config from the Store.
This Config should hold every Information needed to connect to the component. It is not responsible to define "runtime values".

## Triggers
A Collection of Triggers for Azure Components. Any Trigger should provide a fluent API to increase readability.
A Trigger can have an Return Value if it gets all needed Information upon Triggering the Component. Otherwise it should not wait or block for any Return Value.

### FunctionApp
Supported RunTime: dotnet-isolated
#### InProcess
The InProcess FunctionApp Trigger should provide a way to call a FunctionApp Trigger in process. 
This should cause the Trigger to run in the test process thus providing more insights into the error Status and general state of the Trigger.

#### HTTP
#### Timed

### LogicApp
TODO: Think about what makes sense
#### HTTP
#### Timed

### ServiceBus
The ServiceBus Trigger will send or schedule a new Message to a Queue or Topic. It should return any relevant Information to make it easy to find the Message.

## Artifacts
A Collection of Artifacts managed by Azure. A Artifact in the Azure Context should have a Azure Component and a fixed and unique Identifier to manage it.

### DB
#### CosmosDb
#### SQL
#### EF-Core

### StorageAccount
#### Blob
#### Table
#### Queue
TODO: Think about if it makes sense

## Events
### ServiceBus
The ServiceBus Event waits for a new Message on a Queue or Subscription. 
It should provide Options to create a Temp subscription with strict filter options to ensure that only the desired Message is being acted on.
It should provide the possibility to Filter Messages after receiving them in case a Temp subscription is not an option.

### LogicApp
The LogicApp Event is for Monitoring of the Status for a provided Run. It exist to complete the life-cycle of an LogicApp Run in combination with the LogicApp-Trigger.

## Server-like Trigger
It should be checked (by Ping) if the Server is even Online before Sending the request.