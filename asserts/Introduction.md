This plugin retains historical numerical and string data from devices, making it accessible in the User interface through tables and graphs. Additionally, you can generate statistics, such as the average values over a specific time period. Furthermore, this plugin facilitates the creation of statistical devices that can be leveraged to enhance your events and automation.

Features
* The plugin records the historical numerical and string data for all devices, and it is designed to operate efficiently without consuming excessive resources. This plugin addresses the need to easily track device behavior over a specified interval, such as determining when heating was active or how frequently a door was opened, without requiring extensive initial setup.
* You have the option to display the records as a table with the corresponding time duration for a specific interval.
* You can visualize the records in the form of charts, and these charts are zoomable, allowing for increased accuracy as you zoom in.
* You can view statistical functions like maximum, minimum, etc related to the records over a specific interval.
* You can view histogram for the distribution of values. This can tell you information like how long AC was ruuning in last 24 hours.
* The values are stored in an SQLite database, which is self-maintaining and requires no user intervention.
* You have the capability to import the statistical functions of the device over a specified interval as HomeSeer devices. This approach, when integrated with events, provides a more flexible means to utilize statistical metrics like average, maximum, minimum, and other similar functions. This versatility enhances your ability to harness and utilize these statistical insights within your HomeSeer automation setup.
  

FAQ
* What's the process for stopping the tracking of a device's values?

You can select unchecking "Track this device values" in Device Options. The device options can be accessed in Historical Records tab of the device. Device options can also be accessed via Device statistics page. 

* When my device occasionally reports incorrect values, how can I establish a valid range for it?

The device's range can be set using device options. The values outside the range are ignored. The existing values outside the range are deleted from database.

* When I encounter an error like "Sqlite version is too old" upon starting the plugin on a non-Windows system, what steps should I take to resolve it?

On Windows, the sqlite is bundled with plugin. For other systems, the system provided version is used. The sqlite version needs to be above 3.37+.

* How do I go about creating a statistical device associated with a specific device?


* My database has become too large. What methods can I employ to reduce its size?

Open the Plugin->Device Statistics and check which device is adding a large number of records. You can either stop tracking devices which are not required. As alternative, you can reduce the time for retention of records in Plugin Settings page.

* Is there a way to check the size of the database?
The database size is shown in Plugin->Database statistics page.

* What is the duration for which the records are retained?
It is controlled by the setting in the Plugin setting page. Default is 30 days.


* How do I take backup of the database ?
The plugin supports the default backups of Homeseer. The sqlite database should get backed up with homeseer backup.

    
