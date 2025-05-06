# SyncOne

**SyncOne** is a modular and extensible application designed to manage SMS communication and processing in real-time. It supports seamless operation through a combination of background services and user interface integration, ensuring reliable messaging workflows. SyncOne is ideal for scenarios requiring message automation, real-time updates, and platform independence.

---

## Features

- **Background Processing:**
  - A robust foreground service (`SmsService`) processes SMS messages independently of the UI.
  - Ensures messages are sent and processed even when the application is not active.

- **Platform-Agnostic Design:**
  - Uses `ISmsService` as an abstraction layer for SMS operations, enabling seamless integration across platforms.
  - `AndroidSmsService` provides platform-specific implementation for Android.

- **Real-Time Updates:**
  - The UI reflects real-time updates of received and processed messages via `MainViewModel`.

- **Retry Mechanism:**
  - Built-in retry logic ensures reliable SMS delivery with exponential backoff for failed attempts.

- **Modular Architecture:**
  - Components like `DatabaseService`, `ApiService`, and `ConfigurationService` encapsulate specific responsibilities, promoting maintainability.

---

## Components

### 1. **AndroidSmsService**
- **Role:**
  - Handles platform-specific SMS operations such as sending and receiving messages.
  - Implements the `ISmsService` interface.
- **Features:**
  - Real-time SMS receiving using Android broadcast receivers.
  - SMS delivery and sent status tracking using `PendingIntent`.

### 2. **SmsService**
- **Role:**
  - Background service that ensures SMS processing continues even if the app is closed.
- **Features:**
  - Loads unprocessed messages from the database.
  - Uses `ISmsService` for sending SMS.
  - Implements a retry mechanism for failed message deliveries.

### 3. **MainViewModel**
- **Role:**
  - Manages UI state and interactions.
- **Features:**
  - Binds to the message list, reflecting real-time updates from `AndroidSmsService`.
  - Exposes commands like refresh to reload messages from the database.

---

## Prerequisites

1. **Android Development Environment:**
   - Android Studio or equivalent IDE.
   - Android SDK.

2. **Permissions:**
   - Add the following permissions to `AndroidManifest.xml`:
     ```xml
     <uses-permission android:name="android.permission.SEND_SMS"/>
     <uses-permission android:name="android.permission.RECEIVE_SMS"/>
     <uses-permission android:name="android.permission.READ_SMS"/>
     ```

3. **Dependency Injection:**
   - Ensure DI setup (e.g., Microsoft.Extensions.DependencyInjection) for services like `ISmsService`.

---

## Setup and Installation

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/yourusername/SyncOne.git
   ```

2. **Open the Project:**
   - Use Visual Studio or any compatible IDE to open the project.

3. **Configure Dependencies:**
   - Register services in your DI container:
     ```csharp
     services.AddSingleton<ISmsService, AndroidSmsService>();
     services.AddSingleton<DatabaseService>();
     services.AddSingleton<ApiService>();
     services.AddSingleton<ConfigurationService>();
     ```

4. **Build and Run:**
   - Build the project and deploy it to an Android device or emulator.

---

## Usage

### Running the Application
- Start the app to initialize the `SmsService`.
- The background service will handle SMS processing even if the app is minimized or closed.

### Using the UI
- The message list displays incoming and processed messages.
- Use the refresh command to reload messages from the database.

---

## Contributing

Contributions are welcome! To contribute:
1. Fork the repository.
2. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. Commit your changes:
   ```bash
   git commit -m "Add your message here"
   ```
4. Push to your branch:
   ```bash
   git push origin feature/your-feature-name
   ```
5. Open a Pull Request.

---

## License

SyncOne is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

## Contact

For questions or support, reach out to:
- **Email:** tellikoroma@gmail.com
- **GitHub Issues:** [SyncOne Issues](https://github.com/telli/SyncOne/issues)

# SyncOne

SyncOne is a mobile application built with .NET MAUI that enables SMS synchronization and processing, currently implemented for Android.

## Features

- SMS message processing and synchronization for Android
- Background service for continuous operation
- Configurable message filtering
- API integration for message processing
- SMS receiving and sending capabilities
- Integration with Language Models (LLMs) for intelligent message processing

## Use Cases

### SMS-based LLM Interactions
SyncOne enables users to interact with Language Models (LLMs) via SMS, making AI capabilities accessible even without internet access on the user's device. Some applications include:

- AI assistants accessible via SMS
- Information retrieval through text messages
- Language translation services
- Educational content delivery to areas with limited internet connectivity
- SMS-based customer support automation

### Other Use Cases
- Remote data collection in low-connectivity areas
- Two-factor authentication message processing
- Automated response systems
- Community information distribution

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or later with MAUI workload
- Android SDK (API level 21 or higher)

### Installation

1. Clone the repository
   ```
   git clone https://github.com/yourusername/SyncOne.git
   ```

2. Open the solution in Visual Studio

3. Restore NuGet packages

4. Build and deploy to an Android device or emulator

## Configuration

The application uses a SQLite database for local storage. Configuration settings can be adjusted through the application interface.

## Project Structure

- `Models/` - Data models
- `Services/` - Core application services
- `ViewModels/` - MVVM view models
- `Platforms/Android/` - Android-specific implementations including SMS services
- `Platforms/Android/Services/` - Android SMS service implementation

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Author

Telli Koroma

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.