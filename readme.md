## Audio Fingerprinting software written in c# .NET

This software was developed at [www.muziekweb.nl](https://www.muziekweb.nl) by 
Yvo Nelemans. We are a public library in the Netherlands for lending cd's, lp's 
and dvd's. We have a collection of over 500.000 physical objects we lend out to 
our lenders in the Netherlands.


### What does it do?

You can create audio fingerprints using two diffent algorithmes. One algorithm 
is used to find the same audio track using the complete track as input. This is 
based on [AcoustID](https://acoustid.org/) and implemented in this c# .net 
library.
The second algorithm can detect the right track using only 12 to 15 
seconds of audio. Detecting an audio fragment takes around 8-12 seconds on 
avarage in a database of 7,1 millon audio tracks. An SSD is obligatory, using a 
HD with this number of tracks just doesn't work!

This is a toolkit, and is merely an example of what can be done with audio 
detection.

The example programs to create fingerprints, index fingerprints and detect audio 
are simplified versions of what muziekweb uses internally. Most of the programs 
are heavily multithreaded to max out as much as is possible of the CPU. In the 
end storage is still the bottleneck. To really max out the CPU you would probaly 
need to do it al in memory, but because the fingerprint database alone is over 
700 gb this isn't possible for us to test, and you need this data especially for 
the matching part.


### Test audio data

For test data I've included 10 audio tracks from The slip - Nine Inch Nails. They 
actively allow you to share and use the music anyway you want. See also 
[The-slip](https://www.muziekweb.nl/Link/JK147510/Nine-Inch-Nails/The-slip)
Alternativly we make available a dataset of fingerprint of 1,3 millon tracks.


### License

The code is copyrighted 2015-2017 by Stichting Centrale Discotheek Rotterdam, my 
employer, and licensed under the [MIT 
license](https://opensource.org/licenses/MIT). 
Specific parts of the code are written by others and are copyrighted by their 
respective holders.


### History

When developing this software library I first looked at existing software if 
that could do the trick. I tried 
https://github.com/AddictedCS/soundfingerprinting version 2.x which gave good 
results. There was one problem however. The speed with a large audio database 
was just to slow. After some experimenting with the software libary I gave up, 
because I didn't think the way the fingerprint matching was working would scale 
up to our database of aproxmaly 6 million tracks (at that time). 
In the end I used two algorithmes that scale very well with a large audio 
database. The way the software libary is setup you can find some simularities 
with AddictedCS. Disclaimer: The two algorithmes are based on existing software. 
I didn't invent them myself! The first algorithm is 
[AcoustID](https://acoustid.org/) and the second one is based on 
[openfp](http://open-fp.sourceforge.net/).


### When to use which algorithm

We at [muziekweb.nl](https://www.muziekweb.nl) use the acoustID algorithm 
internally when cataloguing tracks from a cd album. Specifically for compilation 
albums. Most of the tracks titles on a compilation album have allready been 
entered into our database on indiviuale albums from the specific artist. When we 
have to catalogue a compilation album we try to detect if the same track 
allready exists on another album and if so use the track titel and artist 
metadata from the existing desciption. This saves us a lot of time.

The second algorithm is used on our website to display what is playing on a 
radio channel. At the moment we follow, 12 different dutch radio channels, of 
which two are classically orientated. Using the data compiled over a large 
timeperiode we are able to tell which artists and albums where popular.


### How does it work

An explanation for the acoustID algorithm can be found at the website 
[https://acoustid.org/](https://acoustid.org/). The second algorithm is 
explained here.

**Fingerprinting**

An audio track is resampled to mono sound with 5512Hz. From this audio a Fast 
Fourier Transformation (FFT) is applied over every 371ms of audio. We then 
calculate a 32-bits value from this data. This is the "subfinger". Then the 
audio data is moved 11,6ms and we again calculate a new "subfinger". We do this 
until the end of the track. We skip only 11,6 ms so the starting time of a 
fragment we try to detect will overlap with our fingerprint data of the whole 
track. The end result is a large collection of 32-bit values we store in a MySQL 
database. This data is "The (sub)Fingerprint" of the audio (see mysql table 
fingerdb.subfingerid). We did this for our entire database of 7,1 million 
audio tracks (as of this moment june 2017).

**Matching**

When we want to match an audio sample against this database we again create a 
fingerprint of the audio sample we try to detect. The individual 32-bit 
fingerprint values are then searched with a specially created inverted database 
to (quickly) find the best possible matching tracks. For the best posssible 
matches we retrieve the entire fingeprint for the audio track from the MySQL 
database and calculate a BER (Bit Error Rate). When the BER is below a specific 
value we count is as a match. The lowest BER value is the best match which is 
then presented.

**Inverted index**

Searching in a database with a very large number of blob data (fingerprints) 
against a large number of values is slow. To solve this we used a Lucene index 
to find the best possible matches fast, before we look at the entire 
fingerprint. See the original java project here 
[Lucene](https://lucene.apache.org/). Creating and maintaining this inverted 
index takes a lot of time and disk (SSD) power.


### Setup used at muziekweb.nl

For our website [muziekweb.nl](https://www.muziekweb.nl) we follow 12 different dutch 
radio channels. To achieve this we have a few servers and Intel NUC's to 
accomplish this.
1x MySQL server, for storing the fingerprints in a blob. (an old dell server)
1x Server with SSD storage, for creating the fingerprints and the inverted index.
   (again  an old dell server, upgraded with SSD's). 
4x NUC's (i7 3,1GHz, 16gb, 1 tb) with SSD storage, to follow the radio channels. 
   Intel NUC NUC5i7RYH

The fingerprint database is updated every week. Updating takes aproximly 24 
hours. Most of the time is needed to update the inverted lucene lookup table. 
After the fingerprints are updated we upload them to the 4 NUC's. Every NUC 
runs a web service which can proces up to 3 fingerprint at a time. Answering 
takes anywhere between 8 to 12 seconds.


### 1,3 million fingerprint database

muziekweb.nl makes the fingerprint data (an MySQL dump and an inverted lucene 
index) available for more than 1,3 million tracks.
Note: It is not possible to recreate the orginal audio from the fingerprint data.

When you want to use this data you may also want a test account for the web service 
muziekweb.nl hosts. The web service is needed to map the found fingerprint back to 
a track title and album desciption. We license our metadata, so access has been 
limited to 1000 requests a day for a maximum of 10.000 requests. If you need 
more requests than a license on our meta database is needed. Please feel free to 
contact us at <info@muziekweb.nl>. For test purposes the 10 audio tracks of "The 
slip - Nine Inch Nails" which are included in the Visual Studio solution do not 
count toward this limit. The metadata is not needed to see if an audio fragment is 
detected, you just don't see what the name of the track is.

Expect a sign up page at www.muziekweb.nl for our web service around the end op august.


### Setting up the MySQL database with existing fingerprints

You can download the MySQL Dump and lucene index from:
ftp.cdr.nl login as anonymous, password doesn't matter
the files can be found in the FingerprintDump map.

Run the script **CreateDatabase.sql**
This will create an EMPTY database "fingerprint" and it's tables and sp's.
Now we restore the fingerprint data
mysql -u username -p dbname < filename.sql

Alternatively you can use the CreateDatabase project in the solution which does the same.


### Technology used

- `Lucene.NET` v3.0.3 [Lucene.net](https://lucenenet.apache.org/)
- `MySQL 5.7.x Community edition` [MySQL](https://dev.mysql.com/downloads/installer/)
- `BASS`, audio library with BASS.Net to interface with it. 
  [Bass](https://www.un4seen.com/)
- `FFTW` there is an c# FFT, implementation in the 
  Fingerprint library, but it's not use because it's slow
  [FFTW](http://www.fftw.org)


### Setup test enviroment

1. Install MySQL 5.7.x
   https://dev.mysql.com/downloads/installer/

   1. Run installer and chose Server only (Or full if you need the client tools)
   2. Chose Standalone MySQL Server
   3. Config type: Chose yourself, I chose Development Machine
   4. Chose a password: 123456 -=> Chose a better one and change it in the source code (.\AudioFingerprinting\SharedCode\Class.DB_Helper.cs)

2. Creating a "clean" database.

   Run the script "CreateDatabase.sql". (.\AudioFingerprinting\DatabaseScripts\CreateDatabase.sql)
   
   For example use https://dev.mysql.com/downloads/workbench/ to run the script.
    
   This will create a database 'FINGERPRINT' with the needed tabels and sp's

   You can also rerun it to reset the database to an empty state.
    
3. Configure .\SharedCode\Fingerprint.ini and .\SharedCode\Class.DB_Helper.cs 
   in the Solution

   Fingerprint.ini -=> This file contains all the settings for the different 
                       programs. Most important is the [MySQL] settings
                       and the location of the Lucene indexes.

                       If you have a BASS.net registration number you can enter 
                       the info in this ini file, otherwhise you'll see a nag 
                       screen from the bass.net dll
      
4. Run the project "CreateAudioFingerprint"
   This will create the fingerprint for the 10 supplied audio files and added them to the 
   MySQL database. Both fingerprint algorithm are used.
    
5. Run the project "CreateInversedFingerprintIndex"
   This will create an inverted lucene index
    
6. Run the project "MatchAudio"
   This will try to match the samples with the created fingerprints.
    

For a unique track reference an crc32 number is used as reference. The CRC32 is 
based on the file name so files with the same name overwrite eachother in the 
fingerprint database. Write your own unique track reference if you need it. For 
this example it is not needed, for large audio databases something else is 
needed.

For reference:

| Filename              | CRC32    | Muziekweb reference
| --------------------- | -------- | -------------------
| JK147510-0001.MP3     | E270180E | 74829A1C77B228BF
| JK147510-0002.MP3     | A5D062DE | 302FF4B87C6B606 
| JK147510-0003.MP3     | 98B04B6E | E4475D91DB27D51B
| JK147510-0004.MP3     | 2A90977E | 73A9C151F8A6FD2B
| JK147510-0005.MP3     | 17F0BECE | 27B213723A828870
| JK147510-0006.MP3     | 5050C41E | 37CA54A2D6832526
| JK147510-0007.MP3     | 6D30EDAE | 82ED646BD7DA842A
| JK147510-0008.MP3     | EF607A7F | FC3E0F7E9073D929
| JK147510-0009.MP3     | D20053CF | 7E1351D8ABD1456D
| JK147510-0010.MP3     | 144CE21B | 3552D854447400F2


### Solution
```
Solution
|
+---Audio                    -> Audio files used to make fingeprints
|   |
|   +Samples                 -> Audio test samples used in "MatchAudio"
|    
+---Externals                -> Externals DLL's need for the different projects
|
+---SharedCode               -> Code shared by multiple projects
|
+---AudioFingerprint         -> .net library for fingerprint creation and matching
|
+---CreateAudioFingerprint   -> Fingerprint creation and storing in database
|
+---CreateDatabase           -> Help program to initalize MySQL database and can load tables with supplied data
|
+---MatchAudio               -> Match/Find audio track based on a audio fragment
|
+---RadioChannel             -> Follows an internet radio channel and tries to identify what's playing (See also mysql.fingerprint.RADIOCHANNEL table)
|
+---RadioChannelWebService   -> Web service which does the actual song detection for RadioChannel (this makes it possible to run up to 3 diferent RadioChannels)
```


### RadioChannelWebService
This program is specially written to handle multiple detection calls for the 
RadioChannel program. To get any performance out of it an SSD for the Lucene 
index with 7 million tracks it absolutly necessary.


### RadioChannel
![Screenshot RadioChannel Console app](https://github.com/nelemans1971/AudioFingerprinting/blob/master/ScreenShotRadioChannel.png?raw=true)

Console application, which opens a radio streams and tries to detect what is 
playing. The radio stream url's are defined in the MySQL database 
fingerprint.radiochannel. 

*eg: radiochannel /RADIO:SKYRADIO*

Don't forget to run RadioChannelWebService first, because it is needed for the detetion.

There are a few test options to turn the audio on and write a WAV file of 15 
second audio from inside the console.


### Example code

Very simplyfied code to detect a piece of audio.
Look for a more comprehensive example in **Project MatchAudio**.

```csharp
// Initalize this class at the beginning of your program
AudioFingerprint.Math.SimilarityUtility.InitSimilarityUtility();
// Use bass for resampling
audioEngine = new AudioEngine();

// Open an lucene index
IndexSearcher indexSubFingerLookup = new IndexSearcher(IndexReader.Open(FSDirectory.Open(new System.IO.DirectoryInfo(@"C:\DB\SubFingerLookup")), true));
// Use special lucene class for simularity calculation
indexSubFingerLookup.Similarity = new CDR.Indexer.SimilarityNoPriority();

// Start a "query" using the above opened index
SubFingerprintQuery query = new SubFingerprintQuery(indexSubFingerLookup);

// Point to a 15 second audio fragment (MUST be a file!)
FingerprintSignature fsQuery = CreateSubFingerprintFromAudio(@"..\..\..\Audio\Samples\JK147510-0002-224Sample-45s-60s.mp3");
// Retrive possible matches
Resultset answer = query.MatchAudioFingerprint(fsQuery);

// Show the matched result
if (answer != null)
{
    Console.WriteLine("======================================================================");
    Console.WriteLine("Algorithm: " + answer.Algorithm.ToString());
    Console.Write("Stats: ");
    Console.Write("Total=" + (answer.QueryTime.TotalMilliseconds / 1000).ToString("#0.000") + "s");
    Console.Write(" | FingerQry=" + (answer.FingerQueryTime.TotalMilliseconds / 1000).ToString("#0.000") + "s");
    Console.Write(" | FingerLD=" + (answer.FingerLoadTime.TotalMilliseconds / 1000).ToString("#0.000") + "s");
    Console.Write(" | Match=" + (answer.MatchTime.TotalMilliseconds / 1000).ToString("#0.000") + "s");
    Console.WriteLine();
    Console.WriteLine();

    foreach (ResultEntry item in answer.ResultEntries)
    {
        Console.WriteLine("SearchPlan  : " + item.SearchStrategy.ToString());
        Console.WriteLine("Reference   : " + item.Reference.ToString());
        // AcoustID is for complete track so position in track is pointless
        if (item.TimeIndex >= 0)
        {
            Console.WriteLine("Position    : " + (item.Time.TotalMilliseconds / 1000).ToString("#0.000") + " sec");
        }
        else
        {
            Console.WriteLine("Position    : Match on complete track");
        }
        if (result.Algorithm == FingerprintAlgorithm.AcoustIDFingerprint)
        {
            Console.WriteLine(string.Format("Match perc. : {0}%", item.Similarity));
        }
        else
        {
            Console.WriteLine("BER         : " + item.Similarity.ToString());
        }
        Console.WriteLine();
    } //foreach
    Console.WriteLine("======================================================================");
}
```


### Questions?
You can contact me (Yvo Nelemans) at y.nelemans@muziekweb.nl
