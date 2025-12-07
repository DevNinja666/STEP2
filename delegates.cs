using System;
using System.Collections.Generic;

namespace SensorNotificationSystem
{
public delegate void SensorChangedHandler(int newValue);

```
public class Sensor
{
    private int _value;
    private List<SensorChangedHandler> subscribers = new List<SensorChangedHandler>();

    public void SetValue(int value)
    {
        _value = value;
        NotifySubscribers();
    }

    public void AddSubscriber(SensorChangedHandler subscriber)
    {
        if (!subscribers.Contains(subscriber))
            subscribers.Add(subscriber);
    }

    public void RemoveSubscriber(SensorChangedHandler subscriber)
    {
        if (subscribers.Contains(subscriber))
            subscribers.Remove(subscriber);
    }

    public void ShowSubscribers()
    {
        Console.WriteLine("Текущие подписчики:");
        for (int i = 0; i < subscribers.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {subscribers[i].Method.Name}");
        }
    }

    private void NotifySubscribers()
    {
        foreach (var sub in subscribers)
        {
            sub(_value);
        }
    }
}

class Program
{
    static void LogChange(int value)
    {
        Console.WriteLine($"[ЛОГ]: Значение изменилось на {value}");
    }

    static void AlertHighValue(int value)
    {
        if (value > 70)
            Console.WriteLine($"[ОПОВЕЩЕНИЕ]: Внимание! Высокое значение: {value}");
    }

    static void AlertLowValue(int value)
    {
        if (value < 30)
            Console.WriteLine($"[ОПОВЕЩЕНИЕ]: Внимание! Низкое значение: {value}");
    }

    static void Main(string[] args)
    {
        Sensor sensor = new Sensor();

        sensor.AddSubscriber(LogChange);
        sensor.AddSubscriber(AlertHighValue);

        while (true)
        {
            Console.WriteLine("\nВведите команду:");
            Console.WriteLine("1. Добавить подписчика");
            Console.WriteLine("2. Удалить подписчика");
            Console.WriteLine("3. Установить новое значение");
            Console.WriteLine("4. Показать список подписчиков");
            Console.WriteLine("5. Выход");

            string input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    Console.WriteLine("Выберите подписчика для добавления:");
                    Console.WriteLine("1. LogChange");
                    Console.WriteLine("2. AlertHighValue");
                    Console.WriteLine("3. AlertLowValue");
                    string addChoice = Console.ReadLine();
                    switch (addChoice)
                    {
                        case "1": sensor.AddSubscriber(LogChange); break;
                        case "2": sensor.AddSubscriber(AlertHighValue); break;
                        case "3": sensor.AddSubscriber(AlertLowValue); break;
                    }
                    break;
                case "2":
                    Console.WriteLine("Выберите подписчика для удаления:");
                    sensor.ShowSubscribers();
                    if (int.TryParse(Console.ReadLine(), out int removeIndex) && removeIndex > 0)
                    {
                        if (removeIndex <= sensor.subscribers.Count)
                            sensor.RemoveSubscriber(sensor.subscribers[removeIndex - 1]);
                    }
                    break;
                case "3":
                    Console.Write("Введите новое значение датчика: ");
                    if (int.TryParse(Console.ReadLine(), out int newValue))
                        sensor.SetValue(newValue);
                    break;
                case "4":
                    sensor.ShowSubscribers();
                    break;
                case "5":
                    return;
            }
        }
    }
}
```

}
