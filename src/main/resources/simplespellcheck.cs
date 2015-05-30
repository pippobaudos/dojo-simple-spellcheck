using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RICS.Utilities
{
    // TODO: Properly internationalise this!
        
    /// <summary>
    /// Implements a simple spell-checker / auto-corrector based on Peter Norvig's algorithm
    /// Some ideas (but not code) taken from Jon Skeet's C# port of Norvig's original Python script.
    /// </summary>
    /// <remarks>
    /// This class is useless without a corpus text file. It will work for any language using the standard Roman alphabet provided a corpus
    /// for that language has been loaded, but insertion / deletion comparison works with the standard English a-z set only, so the usefulness for some languages
    /// such as Polish and Bulgarian that add extra letters to standard Roman is lowered, although it will still basically work.
    /// </remarks>
    public static class SimpleSpellCheck
    {
        private static string _alphabet = "abcdefghijklmnopqrstuvwxyz"; // for inserts / deletes - could get from Culture?
        private static Dictionary<string, int> _wordFrequencies; // a table of words in the corpus and their frequency

        /// <summary>
        /// Reads the corpus file, which should be a plain-text file containing sample text to establish a baseline for known words and word frequency
        /// </summary>
        /// <param name="corpusFilePath">The local path to the corpus file</param>
        /// <remarks>The corpus data is held in RAM until the AppDomain recycles or this method is called again.</remarks>
        public static void BuildCorpus(string corpusFilePath)
        {
            string sample = "";

            // if this file is too big... could be trouble; but for simplicity's sake let's assume we're OK for now
            try
            {
                sample = File.ReadAllText(corpusFilePath);
            }
            catch (Exception ex)
            {
                throw new FileLoadException("Could not load corpus file.", ex);
            }

            BuildCorpusFromString(sample);
        }

        /// <summary>
        /// Builds the corpus data from a string containing sample text to establish a baseline for known words and word frequency
        /// </summary>
        /// <param name="sample">The corpus data</param>
        public static void BuildCorpusFromString(string sample)
        {
            IEnumerable<string> words = GetWordsFromText(sample);
            _wordFrequencies = words.GroupBy(word => word).ToDictionary(group => group.Key, group => group.Count());
        }

        /// <summary>
        /// Finds which words from a given list are known (ie in the dictionary)
        /// </summary>
        /// <param name="words">An input list of words to check</param>
        /// <returns>A list of the words from the input sample that are in the dictionary</returns>
        public static IEnumerable<string> FindKnownWordsFrom(IEnumerable<string> words)
        {
            return words.Where(word => _wordFrequencies.ContainsKey(word.ToLowerInvariant())).Distinct();
        }

        /// <summary>
        /// Finds which words from a given list are not known (ie in the dictionary)
        /// </summary>
        /// <param name="words">An input list of words to check</param>
        /// <returns>A list of the words from the input sample that are not in the dictionary</returns>
        public static IEnumerable<string> FindUnknownWordsFrom(IEnumerable<string> words)
        {
            return words.Where(word => !_wordFrequencies.ContainsKey(word.ToLowerInvariant())).Distinct();
        }

        /// <summary>
        /// Suggests alternatives for a misspelled word
        /// </summary>
        /// <param name="word">The word to check for misspellings</param>
        /// <returns>A list of possible alternatives, in likelihood order based on frequency of use in the corpus data</returns>
        /// <remarks>It's possible to get false positives from this method, as some misspellings of correct words are dictionary words themselves</remarks>
        public static IEnumerable<string> SuggestAlternativesFor(string word)
        {
            if (_wordFrequencies == null) throw new Exception("Corpus not built. Call BuildCorpus() before attempting to call this method.");

            // if the input is a known word already, skip checking for alternatives and return the word itself
            if (FindKnownWordsFrom(new string[] { word }).Count() > 0) return new string[] { word };

            List<string> candidates = new List<string>();
            
            // try to find known words in the first-level set of alternatives
            candidates.AddRange(FindKnownWordsFrom(GenerateSets(word)));
            
            // if that didn't work, try to find known words in the second-level set of alternatives of each element of the first-level set
            if (candidates.Count == 0) 
                candidates.AddRange(FindKnownWordsFrom(GenerateSets(word).SelectMany(alternative => GenerateSets(alternative))));

            // if there's nothing at all, send /*the original word*/ nothing back
            if (candidates.Count == 0)
            {
                return new string[] { /*word*/ };
            }
            else
            {
                // order the results by their frequency in the corpus
                return candidates.OrderByDescending(candidate => _wordFrequencies[candidate.ToLowerInvariant()]);
            }
        }

        /// <summary>
        /// Takes a block of text and replaces all unknown words with the most likely correct word
        /// </summary>
        /// <param name="input">The text to check</param>
        /// <returns>The corrected text</returns>
        /// <remarks>Corrections are made in lower-case regardless of the input case, except for initial capitals which are left intact</remarks>
        public static string AutoCorrect(string input)
        {
            // run the text through Check to get back a list of corrections
            foreach (SpellCheckItem item in Check(input).Where(word => word.SuggestedAlternatives.Count() > 0))
            {
                // need to respect the casing of the original if the first letter of a word was upper-cased 
                MatchCollection originals = Regex.Matches(input, Regex.Escape(item.SuspectedWord), RegexOptions.IgnoreCase);
                
                // using Regex.Replace as string.Replace is case-sensitive
                input = Regex.Replace(input, Regex.Escape(item.SuspectedWord), item.SuggestedAlternatives.First(), RegexOptions.IgnoreCase);
                MatchCollection replacements = Regex.Matches(input, Regex.Escape(item.SuggestedAlternatives.First()), RegexOptions.IgnoreCase);

                // restore initial caps where needed
                for (int i = 0; i < originals.Count; i++)
                {
                    if (Char.IsUpper(originals[i].Value[0]))
                        input = input.Substring(0, replacements[i].Index) + replacements[i].Value.Substring(0, 1).ToUpperInvariant() + input.Substring(replacements[i].Index + 1);
                }
            }

            return input;
        }

        /// <summary>
        /// Checks a block of text and returns a list of object identifying possible misspellings and alternatives
        /// </summary>
        /// <param name="input">The text to check</param>
        /// <returns><![CDATA[An IList<SpellCheckItem> identifying unknown words and likely alternatives]]></returns>
        public static IList<SpellCheckItem> Check(string input)
        {
            List<SpellCheckItem> items = new List<SpellCheckItem>();
            foreach (string word in FindUnknownWordsFrom(GetWordsFromText(input)).Distinct())
            {
                items.Add(new SpellCheckItem() { SuspectedWord = word, SuggestedAlternatives = SuggestAlternativesFor(word) });
            }

            return items;
        }

        /// <summary>
        /// Slices text into individual words, ignoring non-alphabetical characters and converting to lower-case
        /// </summary>
        /// <param name="text">Text to slice</param>
        /// <returns>List of words</returns>
        /// <remarks>This is not a distinct select - repeated words will repeat in the list. Use .Distinct() to get a distinct set.</remarks>
        private static IEnumerable<string> GetWordsFromText(string text)
        {
            // matches only letters a-z (hence why text.ToLower); will not match accented letters etc
            return Regex.Matches(text.ToLowerInvariant(), "[a-z]+").Cast<Match>().Select(match => match.Value);
        }

        /// <summary>
        /// Generates a set of possible alternatives for a given word
        /// </summary>
        /// <param name="word">The word to check</param>
        /// <returns>Set of all alternatives</returns>
        private static IEnumerable<string> GenerateSets(string word)
        {
            // possible misspellings include missing letters (today -> tday), transposed letters (the -> teh), replacements (date -> dzte) and insertions (date -> dzate)
            // this method uses a set of slices through the word which are used as the basis for generating several set of possible variants to check through:
            // so for example 'date' would be sliced into {'d','ate'}; {'da','te'}; {'dat','e'}.

            // (if it seems counter-intuitive to call this function for a word that is already misspelled, look at it this way - you suppose call this function with the word
            // 'bashop' when you really mean 'bishop'; 'bishop' will be in the set 'replacements' and will most likely bubble up to the top based on the corpus frequency data)
            
            var slices = Enumerable.Range(0, word.Length + 1).Select(index => new { Left = word.Substring(0, index), Right = word.Substring(index) });

            // remove a letter from the right-hand-side of each slice to simulate missing letters
            IEnumerable<string> deletions = slices.Where(slice => slice.Right.Length > 0).Select(slice => slice.Left + slice.Right.Substring(1));
            
            // swap the first and second chars of the right hand side of each slice to simulate transpositions
            IEnumerable<string> transpositions = slices.Where(slice => slice.Right.Length > 1).Select(slice => slice.Left + slice.Right[1] + slice.Right[0] + slice.Right.Substring(2));
            
            // replace each character in the word with all characters from the standard alphabet, one at a time
            IEnumerable<string> replacements = Enumerable.Range(0, _alphabet.Length - 1).SelectMany(index => slices.Where(slice => slice.Right.Length > 0).
            Select(slice => slice.Left + _alphabet.Substring(index, 1) + slice.Right.Substring(1)));
            
            // insert each of the characters of the alphabet in each position of the word
            IEnumerable<string> insertions = Enumerable.Range(0, _alphabet.Length - 1).SelectMany(index => slices.Where(slice => slice.Right.Length > 0).
            Select(slice => slice.Left + _alphabet.Substring(index, 1) + slice.Right));

            // combine all the sets and return as the total set of possible alternatives for this word
            return deletions.Union(transpositions.Union(replacements.Union(insertions))).Distinct();
        }
    }

    /// <summary>
    /// Encapsulates an individual spell-check data point. Lists the suspected invalid word and suggested replacements.
    /// </summary>
    public class SpellCheckItem
    {
        public string SuspectedWord;
        public IEnumerable<string> SuggestedAlternatives;
    }
}
