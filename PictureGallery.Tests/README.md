# Picture Gallery Tests

## Huidige Status

Het test project is opgezet, maar kan momenteel niet direct refereren naar het hoofdproject vanwege incompatibele target frameworks:
- Hoofdproject: `net8.0-android`, `net8.0-ios`, `net8.0-maccatalyst`, `net8.0-windows`
- Test project: `net8.0` (standaard .NET)

## Oplossingen

### Optie 1: Shared Library (Aanbevolen - Lange termijn)

Maak een `PictureGallery.Core` class library met:
- Alle Models
- Service interfaces en implementaties  
- Business logic

Referentie vanuit:
- `PictureGallery.App` (MAUI project)
- `PictureGallery.Tests` (test project)

### Optie 2: Source Linking (Voor nu)

Gebruik source linking om de source files te delen zonder kopiëren.

### Optie 3: Compiled DLL Reference

Reference de compiled DLL na build (vereist build orchestration).

## Huidige Workaround

Voor nu kunnen we:
1. Test helpers en infrastructure testen (TestBase, TestHelpers)
2. Pure business logic testen zonder MAUI dependencies
3. Later refactoring naar shared library

## Volgende Stappen

1. ✅ Test infrastructure opgezet
2. ⏳ Shared library maken met testbare code
3. ⏳ Tests schrijven voor Core library
4. ⏳ Integration tests voor ViewModels met mocking


