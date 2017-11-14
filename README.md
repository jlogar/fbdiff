# fbdiff

## about

fbdiff is a [Firebird](https://firebirdsql.org/) database diff calculator. **WIP!!!**

Need a compare tool for a project. Since there's none I could find that would fit my needs (apart from the commercial ones like ) I'm writing my own.

Expect to support
- fields (type, length, nullability(?))
- PKs
- FKs
- ...

I'd also like to have a way to offer a model of the src/trg databases, not only a change script.

## alternatives

- [ibexpert](http://ibexpert.net/ibe/pmwiki.php?n=Doc.DatabaseComparer)
- [SO on the topic](https://stackoverflow.com/questions/1233980/firebird-database-schema-data-difference-tool)
- non FB specific ones?
- and http://www.firebirdfaq.org/faq210/
