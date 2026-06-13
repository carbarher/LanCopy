using Xunit;

// ShareRoot mantiene estado estatico (raiz compartida); serializamos los tests
// para evitar carreras entre clases que modifican la raiz.
[assembly: CollectionBehavior(DisableTestParallelization = true)]