EZData
======

A very simple object relational mapping library, built on top of devart's [dotConnect for Oracle](http://www.devart.com/dotconnect/oracle/).  Oracle only for now, looking to add support for others.


## Examples

The examples listed below are meant to be a quick look at how things can be done with EZData and are not indicitive of any sort of best practices when manipulating data in a database.  

### Selecting
---
```c#
using System;
using EZData;

namespace ExampleApp
{
    // EZData Model
    class People : EZData.DBTable
    {
        [EZData.PrimaryKey]
        public Decimal Id { get; set; }
        public String Last { get; set; }
        public String First { get; set; }
        public Decimal Age { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Database.OpenConnection("username", "password", "database");

            var people = Database.Query<People>("select * from people where age > 10");

            foreach (var person in people)
                Console.WriteLine(person.Last + ", " + person.First);
        }
    }
}
```

### Updating
---
```c#
var people = Database.Query<People>("select * from people");

foreach (var person in people)
{
    person.Last = "Test";
    person.Save();    // record is updated
}
```

### Inserting
---
```c#
var bob = new People { Id = 10, First = "Bob", Last = "Test", Age = 12 };

bob.Save();   // record is inserted
```

### Deleting
---
```c#
var bob = Database.Query<People>("select * from people where first = 'Bob' and last = 'Test'");

bob.Delete()  // bye bye bob (record is deleted).
```
