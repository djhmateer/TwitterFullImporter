using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;

namespace RabbitDemo
{
    class Program
    {
        static void Main()
        {
            // Put something on a Queue
            //Send();
            //Receive();

            SendTaskQueue();
            ReceiveTaskQueue();
        }

        static void Send()
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                // declaring a queue is idempotent - will only be created if doesn't exist already
                channel.QueueDeclare(queue: "TestQueue", durable: false,
                    exclusive: false, autoDelete: false, arguments: null);

                string message = "Hello World!";
                var body = Encoding.UTF8.GetBytes(message);
                channel.BasicPublish(exchange: "", routingKey: "TestQueue",
                    basicProperties: null, body: body);
            }
            Console.WriteLine("Done - put message on Queue");
        }

        static void Receive()
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "TestQueue", durable: false, exclusive: false,
                    autoDelete: false, arguments: null);

                var consumer = new EventingBasicConsumer(channel);

                // every time a message is received, this lambda expression (anon method) is called
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body;
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine(" Received from queue {0}", message);
                };
                channel.BasicConsume(queue: "TestQueue", noAck: true, consumer: consumer);

                Console.WriteLine("waiting");
                Console.ReadLine();
            }
        }

        static void SendTaskQueue()
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "task_queue", durable: true, exclusive: false,
                    autoDelete: false, arguments: null);

                for (int i = 0; i < 5; i++)
                {
                    var message = $"Task {i}";
                    var body = Encoding.UTF8.GetBytes(message);

                    var properties = channel.CreateBasicProperties();
                    // notice I have to make the message persistent too to make it durable through a restart
                    properties.Persistent = true;
                    channel.BasicPublish(exchange: "", routingKey: "task_queue",
                        basicProperties: properties, body: body);
                    Console.WriteLine($" Sent to task_queue {message}");
                }
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }

        static void ReceiveTaskQueue()
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "task_queue", durable: true, exclusive: false,
                    autoDelete: false, arguments: null);

                // only receive 1 message at a time, and wait for Ack
                channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                Console.WriteLine(" [*] Waiting for messages.");

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body;
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine(" [x] Received {0}", message);

                    Thread.Sleep(1000);

                    Console.WriteLine(" [x] Done");

                    //if (message == "Task 3")
                    //    throw new Exception("Blowing up to see if Task 3 remains on the queue to be processed later");

                    // must send Ack to say we are done with this message and it can be deleted from Queue
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

                    // could reject it
                    //channel.BasicReject(deliveryTag: ea.DeliveryTag, requeue: true);

                };

                // noAck was true above.. now we need Ack
                channel.BasicConsume(queue: "task_queue", noAck: false, consumer: consumer);

                Console.WriteLine(" Press [enter] to exit.");
                Console.ReadLine();
            }
        }
    }
}
