#!/usr/bin/groovy

def label = "worker-${UUID.randomUUID().toString()}"

def helmLint(String chartDir) {
    println "校验 chart 模板"
    sh "helm lint ${chartDir}"
}

def getNonReleaseVersion() {
    def gitCommit = sh(returnStdout: true, script: 'git rev-parse HEAD').trim()
    def versionNumber;
    if (gitCommit == null) {
        versionNumber = env.BUILD_NUMBER;
    } else {
        versionNumber = gitCommit.take(8);
    }
    print 'build  versions...'
    print versionNumber
    return versionNumber
}


def helmDeploy(Map args) {
    helmLint(args.chartDir)
    if (args.dryRun) {
        println "Running dry-run deployment"
        sh "helm upgrade --dry-run --install ${args.name} ${args.chartDir}  --namespace=${args.namespace} --set image.imageTag=${args.imageTag}"
    } else {
        println "helm upgrade --install  --wait ${args.name} ${args.chartDir}  --namespace=${args.namespace} --set image.imageTag=${args.imageTag}"
        sh "helm upgrade --install  --wait ${args.name} ${args.chartDir}  --namespace=${args.namespace} --set image.imageTag=${args.imageTag}"

        sleep(20)

        echo "Application ${args.name} successfully deployed. Use helm status ${args.name} to check"
    }
}


// INPUT PARAMETERS
properties([
    parameters([
        gitParameter(name: 'BRANCH_NAME', defaultValue: 'master', selectedValue: 'DEFAULT', type: 'PT_BRANCH'),
        choice(name: 'ENVIRONMENT',choices: ['Dev','Beta','Production'],defaultValue: 'Dev',description: 'Select Kubernetes Cloud'),
   ])
])
podTemplate(label: label, serviceAccount: 'jenkins', cloud:params.ENVIRONMENT,containers: [
  containerTemplate(name: 'netcore21', image: 'mcr.microsoft.com/dotnet/core/sdk:2.1', ttyEnabled: true),
  containerTemplate(name: 'docker', image: 'docker:18.09.6', command: 'cat', ttyEnabled: true),  
  containerTemplate(name: 'semantic-release', image: 'morining/semantic-release:1.0.0', command: 'cat', ttyEnabled: true),  
  containerTemplate(name: 'helm', image: 'lachlanevenson/k8s-helm:v2.14.1', command: 'cat', ttyEnabled: true), 
  containerTemplate(name: 'jnlp', image: 'jenkinsci/jnlp-slave:3.29-1')
],
volumes: [
  hostPathVolume(mountPath: '/var/run/docker.sock', hostPath: '/var/run/docker.sock'),
  hostPathVolume(mountPath: '/home/jenkins/.nuget/packages', hostPath: '/home/.nuget/packages/')
]){
    node(label) {
    println "你选择的环境是:${params.ENVIRONMENT}"
    stage('check out') {
        checkout scm
        sh "git checkout -b ${params.BRANCH_NAME}" 
    }
         
        def dockerImageName ="cs-kube-consul-sync-srv"
        def dockerRegistry ="dockerhub.followme-internal.com"
        def dockerRepo = "deploy"
        
        def pwd = pwd()
        def chartDir = "${pwd}/charts/followme-srv-kube-consul-sync"
        def versionNumber = "1.0.0"
        def imageTag = "v" + versionNumber
        def registryCredsId = "docker_followme_regirstry_creds"

        def kubeNamespace = "dotnet"
        def helmAppName = "followme-srv-kube-consul-sync"
  
    stage('unit test') { 
    
    }

    stage('build'){
       
        container('netcore21') {
        sh """
        cd src/FM.Kube.Consul.Sync.Host
        dotnet restore
        dotnet build
        dotnet publish -c Release -o publish 
        """
    }
  }
    stage("versioning"){
         container("semantic-release"){
             withCredentials([string(credentialsId: 'github_token', variable: 'GITHUB_TOKEN')]){
                if(params.BRANCH_NAME == 'origin/master'){
                    sh """
                    export GITHUB_TOKEN=$GITHUB_TOKEN
                    export GIT_BRANCH=master
                    npx semantic-release --no-ci
                    """
                    if(fileExists('.next-version')){
                        versionNumber =  sh(script: 'cat .next-version',returnStdout: true).trim()
                    }else{
                        versionNumber =  sh(script: 'git describe --tags $(git rev-list --tags --max-count=1)',returnStdout: true).trim()
                    }
                    versionNumber = versionNumber.replaceAll("v","")
                    imageTag = "v" + versionNumber
                }else{
                    versionNumber =  getNonReleaseVersion()
                    imageTag = "v" + versionNumber
                }  
            }      
        }
    }

    stage("docker build && docker push"){
        container('docker') {
            withCredentials([[$class          : 'UsernamePasswordMultiBinding', credentialsId: registryCredsId,
            usernameVariable: 'USERNAME', passwordVariable: 'PASSWORD']]) {
            sh "docker login -u ${env.USERNAME} -p ${env.PASSWORD} ${dockerRegistry}"
            println "登陆docker registry 成功！"
            sh """
            docker --version
            docker build -t ${dockerRegistry}/${dockerRepo}/${dockerImageName}:${imageTag} -t ${dockerRegistry}/${dockerRepo}/${dockerImageName}:latest .                            
            docker push ${dockerRegistry}/${dockerRepo}/${dockerImageName}:${imageTag}
            docker push ${dockerRegistry}/${dockerRepo}/${dockerImageName}:latest
            """
            }
        }
    }

    stage("deploy"){
        container('helm') {
        helmDeploy(
        chartDir:chartDir,
        namespace:kubeNamespace,
        imageTag:imageTag,
        dryRun:false,
        name:helmAppName)}
    }

}
}