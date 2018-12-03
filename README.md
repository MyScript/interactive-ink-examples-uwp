## What is it about?

Interactive Ink SDK is the best way to integrate handwriting recognition capabilities into your UWP application. Interactive Ink extends digital ink to allow users to more intuitively create, interact with, and share content in digital form. Handwritten text, mathematical equations or even diagrams are interpreted in real-time to be editable via simple gestures, responsive and easy to convert to a neat output.

This repository contains a "get started" example, a complete example and a reference implementation of the UWP integration part that developers using Interactive Ink SDK can reuse inside their projects. All of those are written in C# and XAML.

The repository content targets UWP platform.

## Getting started

### Prerequisites
This getting started section has been tested with Visual Studio 2015 and Visual Studio 2017 and supports [UWP Build 10240](https://docs.microsoft.com/en-us/windows/uwp/updates-and-versions/choose-a-uwp-version)

### Installation

1. Clone the examples repository  `git clone https://github.com/MyScript/interactive-ink-examples-uwp.git`

2. Claim a certificate to receive the free license to start develop your application by following the first steps of [Getting Started](https://developer.myscript.com/getting-started)

3. Copy this certificate to `GetStarted\MyCertificate.cs` and `Demo\MyCertificate.cs`

4. Open either `MyScript.InteractiveInk.Examples.Uwp-VS2015.sln` or `MyScript.InteractiveInk.Examples.Uwp-VS2017.sln` file. `GetStarted` project is the most simple example and is design to help you understand what Interactive Ink is about and how easy it is to integrate it into your application. `Demo` project contains a complete example and helps you build your own integration. You can select which project to launch by right-clicking the project in the solution browser and selecting "Set as startup project".

5. Note that you may encounter compile errors of the form `...\UserControls\EditorUserControl.xaml.cs(85,27,85,40): error CS0103: The name 'captureCanvas' does not exist in the current context`. When this happen you have to edit `UserControls\EditorUserControl.xaml` file from Visual Studio, add a space at the beginning, remove the space, and save the file. Then build again and the errors should be gone.

## Building your own integration

In your application add the dependency to `MyScript.InteractiveInk.Uwp` nuget. Also copy `UIReferenceImplementation` directory into your project. More details available in the [developer guide](https://developer.myscript.com/docs/interactive-ink/latest/windows/).

## Documentation

A complete guide is available on [MyScript Developer website](https://developer.myscript.com/docs/interactive-ink/latest/windows/).

The API Reference is available in Visual Studio as soon as the Nugets packages are downloaded.

## Getting support

You can get some support from the dedicated section on [MyScript Developer website](https://devportal.corp.myscript.com/support/).

## Sharing your feedback ?

Made a cool app with Interactive Ink? Ready to cross join our marketing efforts? We would love to hear about you!
We’re planning to showcase apps using it so let us know by sending a quick mail to [myapp@myscript.com](mailto://myapp@myscript.com).

## Contributing

We welcome your contributions:
If you would like to extend those examples for your needs, feel free to fork it!
Please sign our [Contributor License Agreement](CONTRIBUTING.md) before submitting your pull request.
