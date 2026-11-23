// Load the current CICD_Platform repository explicitly without modifying the
// Jenkins global 'cicd-platform' library used by legacy BonakMarket jobs.
// Pin both the dynamically loaded library and its CLI tooling to the same revision.
def platformRevision = '1e2c209528818f5940c63998668cdeac4bffb66e'
env.CICD_PLATFORM_REPO_URL = 'https://github.com/behnamaltafi/CICD_Platform.git'
env.CICD_PLATFORM_REVISION = platformRevision
env.CICD_PLATFORM_READER_ID = 'platform-github-read'
env.CICD_SOURCE_READ_ID = 'bonakmarket-github-read'

library identifier: "bonakmarketfamily-platform@${platformRevision}",
    retriever: modernSCM([
        $class: 'GitSCMSource',
        remote: 'https://github.com/behnamaltafi/CICD_Platform.git',
        credentialsId: 'platform-github-read'
    ])

platformCi(configuration: 'cicd/project.json')
