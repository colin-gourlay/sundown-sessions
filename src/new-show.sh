#!/bin/bash

# Usage:
# ./new-show.sh '21' \
# '5th June 2024' \
# 'the-big-now' \
# 'The Big Now' \
# 'IST IST' \
# 'Becky Becky' \
# 'Nick Cave & The Bad Seeds' \
# 'The Filthy Tongues' \
# '2024-06-05T22:00:00Z'

SHOW_NUMBER=$1
BROADCAST_DATE=$2
FEATURED_GUEST_SLUG=$3
FEATURED_GUEST=$4
FEATURED_ARTIST_1=$5
FEATURED_ARTIST_2=$6
FEATURED_ARTIST_3=$7
FEATURED_ARTIST_4=$8
SHOW_DATE_TIME=$9

mkdir "./content/shows/$SHOW_NUMBER/"

cat << EOF > ./archetypes/default.md
---
title: 'Show #$SHOW_NUMBER: Broadcast $BROADCAST_DATE'
slug: 'featuring-$FEATURED_GUEST_SLUG'
description: 'featuring $FEATURED_GUEST'
summary: 'Live on K107, THE SUNDOWN SESSIONS returns with...

          - $FEATURED_ARTIST_1

          - $FEATURED_ARTIST_2

          - $FEATURED_ARTIST_3

          - $FEATURED_ARTIST_4

          - and much, much more...
'
keywords: 
  - '$FEATURED_GUEST'
  - '$FEATURED_ARTIST_1'
  - '$FEATURED_ARTIST_2'
  - '$FEATURED_ARTIST_3'
  - '$FEATURED_ARTIST_4'
toc: true
featured_image: '$SHOW_NUMBER-show-logo.jpeg'
read_more_copy: Show notes...
show_reading_time: true
date: $SHOW_DATE_TIME
draft: false
---

## Playlist
{{< include_content "/shows/$SHOW_NUMBER/playlist" >}}

---

## Featured guest: $FEATURED_GUEST
{{< include_content "/shows/$SHOW_NUMBER/featured-guest" >}}

---

## Show discussion points
{{< include_content "/shows/$SHOW_NUMBER/discussion-points" >}}

---

## Track info
{{< include_content "/shows/$SHOW_NUMBER/track-info" >}}

EOF

cat << EOF > "./content/shows/$SHOW_NUMBER/playlist.md"
1. {{< artist-wikilink "[ARTIST]" >}} - [Track]

- ADVERTISING BREAK

1. {{< artist-wikilink "David Latto" >}} - Geordie Munro

- ADVERTISING BREAK

1. {{< artist-wikilink "[ARTIST]" >}} - [Track]

- NEWS

1. {{< artist-wikilink "[ARTIST]" >}} - [Track]

- ADVERTISING BREAK

1. {{< artist-wikilink "[ARTIST]" >}} - [Track]

- ADVERTISING BREAK

1. {{< artist-wikilink "[ARTIST]" >}} - [Track]
2. {{< artist-wikilink "The Filthy Tongues" >}} - Nae Tongues

EOF

cat << EOF > "./content/shows/$SHOW_NUMBER/featured-guest.md"
#
{{< figure src="$SHOW_NUMBER-guest-logo.jpeg" title="$FEATURED_GUEST" alt="$FEATURED_GUEST" width="75%" >}}

### Contact details

- **email:** [email@example.com](mailto:)
- **phone:** +1 (234) 567-8910
- **website:** [www.example.com]()

### Social Media

- [Facebook]()
- [Instagram]()
- [Twitter]()
- [LinkedIn]()
- [YouTube]()

EOF

cat << EOF > "./content/shows/$SHOW_NUMBER/discussion-points.md"
#

EOF

cat << EOF > "./content/shows/$SHOW_NUMBER/track-info.md"
| Artist                        | Track                                | Duration | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
|-------------------------------|--------------------------------------|----------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

EOF

hugo new "shows/$SHOW_NUMBER/index.md"
