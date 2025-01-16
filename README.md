This simple console application requests two-line element sets (TLE) for given space satellites and saves it to the file.

You can manage satellites by configuration file.

First line of the config file is the API base address. You shouldn't change it usually.

Second line of the file is the default API key. It's strongly recommended register on the website "n2yo(dot)com/api/" and get your own key due to limitations of amount requests.

To personalize list of the satellites, add your desired satellite IDs at line 3 and further. As an example, there are three satellite IDs added by default.

Features:
* Request two-line element sets (TLE) for given satellites.
* Save TLEs to the file.
* Configurable config.
