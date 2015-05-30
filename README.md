Simple Spellcheck
=================

The goal of this kata is to create an idiomatic C# (or Java or PHP) implementation of Peter Norvig's simple spellcheck algorithm. 

You'll need to download the corpus.txt file which you'll find in the root of this folder. This contains a variety of text sources which are essential for this problem.

Also in the root you'll find spellcheck.py, which is Norvig's original Python implementation of this algorithm. You can use this to guide you as to the implementation, but remember that your code should be idomatic in the language you're writing in, not a direct port of the Python code. 

In the first 5 minutes or so of the session, we'll go through this code and the algorithm required, but basic details can be found below along with the user stories.

The algorithm
-------------

The algorithm works like this: 

First, you load the corpus text. This is just a large amount of standard English text. You identify the set of words in the text and combine this with a word count (for example, for 'the', count all the instances of 'the' in the text). This gives you a list of dictionary words and a number indicating how common they are. This will be used to predict the most likely alternate spelling for misspelled words.

You identify misspelled words in a block of text by matching all words that are not in the dictionary list (provided by the corpus file). For each word, you then generate a set of possible alternate spellings, and see which of these match the dictionary. Then you offer those matching words as the possible correct spellings.

To generate the set of alternate spellings, we consider four cases: substitions, transpositions, insertions and deletions. 

For example, take the word 'farmer'. 

- Substitution: fwrmer, rarmer, farner - one letter substitued by another
- Transposition: farmre, famrer, afrmer - a pair of letters swaps
- Insertions: farmner, fasrmer, farmert - an extra letter inserted
- Deletions: farmr, frmer, armer - a letter deleted

The total set of alternate spellings for a word consists of all combinations (a-z, assuming a simple unaccented Roman alphabet) in all positions for all four cases. This is a very large set, but we can then limit it to only those words that match entries in the dictionary.

Stories
-------

**Story 1**

As a user, when I submit a block of text for processing, I want to receive a list of words in that block that are not recognised as dictionary words.

**Story 2**

As a user, when I pass an unrecognised word, I want to receive a list of all the possible dictionary words that are within a single generation of alternatives that are dictionary words.

**Story 3**
As a user, when I submit a block of text for processing, I want to receive back that block of text with all unrecognised words replaced by the most likely alternate dictionary word, where one exists, or the original unrecognised word otherwise.
